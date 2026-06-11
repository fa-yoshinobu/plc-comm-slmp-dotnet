using System.Buffers.Binary;
using System.Text;

namespace PlcComm.Slmp;

internal static class SlmpPayloads
{
    internal static int DeviceSpecSize(SlmpCompatibilityMode compatibilityMode)
        => compatibilityMode == SlmpCompatibilityMode.Legacy ? 4 : 6;

    internal static int EncodeDeviceSpec(
        SlmpDeviceAddress device,
        Span<byte> output,
        SlmpCompatibilityMode compatibilityMode)
    {
        if (compatibilityMode == SlmpCompatibilityMode.Legacy)
        {
            output[0] = (byte)(device.Number & 0xFF);
            output[1] = (byte)((device.Number >> 8) & 0xFF);
            output[2] = (byte)((device.Number >> 16) & 0xFF);
            output[3] = (byte)((ushort)device.Code & 0xFF);
            return 4;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(output[..4], device.Number);
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(4, 2), (ushort)device.Code);
        return 6;
    }

    internal static SlmpExtensionSpec ResolveEffectiveExtension(SlmpQualifiedDeviceAddress device, SlmpExtensionSpec extension)
    {
        var result = extension;
        if (device.ExtensionSpecification is not null && device.ExtensionSpecification.Value != result.ExtensionSpecification)
            result = result with { ExtensionSpecification = device.ExtensionSpecification.Value };
        if (device.DirectMemorySpecification is not null && device.DirectMemorySpecification.Value != result.DirectMemorySpecification)
            result = result with { DirectMemorySpecification = device.DirectMemorySpecification.Value };
        return result;
    }

    internal static byte[] BuildReadWritePayloadExtended(
        SlmpDeviceAddress device,
        ushort points,
        IReadOnlyList<ushort>? values,
        SlmpExtensionSpec extension,
        bool bitUnit,
        SlmpCompatibilityMode compatibilityMode
    )
    {
        var extendedSpec = EncodeExtendedDeviceSpec(device, extension, compatibilityMode);
        var writeBytes = values is null ? 0 : bitUnit ? (values.Count + 1) / 2 : values.Count * 2;
        var payload = new byte[extendedSpec.Length + 2 + writeBytes];
        extendedSpec.CopyTo(payload, 0);
        var offset = extendedSpec.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), points);
        offset += 2;
        if (values is null) return payload;

        if (bitUnit)
        {
            var idx = 0;
            while (idx < values.Count)
            {
                var high = values[idx] != 0 ? 0x10 : 0x00;
                idx++;
                var low = idx < values.Count && values[idx] != 0 ? 0x01 : 0x00;
                if (idx < values.Count) idx++;
                payload[offset++] = (byte)(high | low);
            }
            return payload;
        }

        foreach (var value in values)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), value);
            offset += 2;
        }

        return payload;
    }

    internal static byte[] BuildExtendedRandomReadPayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices,
        SlmpCompatibilityMode compatibilityMode
    )
    {
        var encodedWords = new byte[wordDevices.Count][];
        var encodedDwords = new byte[dwordDevices.Count][];
        var size = 2;
        for (var i = 0; i < wordDevices.Count; i++)
        {
            var (device, extension) = wordDevices[i];
            var spec = EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension), compatibilityMode);
            encodedWords[i] = spec;
            size += spec.Length;
        }

        for (var i = 0; i < dwordDevices.Count; i++)
        {
            var (device, extension) = dwordDevices[i];
            var spec = EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension), compatibilityMode);
            encodedDwords[i] = spec;
            size += spec.Length;
        }

        var payload = new byte[size];
        payload[0] = (byte)wordDevices.Count;
        payload[1] = (byte)dwordDevices.Count;
        var offset = 2;
        foreach (var spec in encodedWords)
        {
            spec.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += spec.Length;
        }
        foreach (var spec in encodedDwords)
        {
            spec.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += spec.Length;
        }
        return payload;
    }

    internal static byte[] BuildExtendedRandomWordWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value, SlmpExtensionSpec Extension)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value, SlmpExtensionSpec Extension)> dwordEntries,
        SlmpCompatibilityMode compatibilityMode
    )
    {
        var encodedWords = new byte[wordEntries.Count][];
        var encodedDwords = new byte[dwordEntries.Count][];
        var size = 2 + (wordEntries.Count * 2) + (dwordEntries.Count * 4);
        for (var i = 0; i < wordEntries.Count; i++)
        {
            var (device, _, extension) = wordEntries[i];
            var spec = EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension), compatibilityMode);
            encodedWords[i] = spec;
            size += spec.Length;
        }

        for (var i = 0; i < dwordEntries.Count; i++)
        {
            var (device, _, extension) = dwordEntries[i];
            var spec = EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension), compatibilityMode);
            encodedDwords[i] = spec;
            size += spec.Length;
        }

        var payload = new byte[size];
        payload[0] = (byte)wordEntries.Count;
        payload[1] = (byte)dwordEntries.Count;
        var offset = 2;
        for (var i = 0; i < wordEntries.Count; i++)
        {
            var spec = encodedWords[i];
            spec.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += spec.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), wordEntries[i].Value);
            offset += 2;
        }

        for (var i = 0; i < dwordEntries.Count; i++)
        {
            var spec = encodedDwords[i];
            spec.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += spec.Length;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), dwordEntries[i].Value);
            offset += 4;
        }

        return payload;
    }

    internal static byte[] BuildExtendedRandomBitWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, bool Value, SlmpExtensionSpec Extension)> bitEntries,
        SlmpCompatibilityMode compatibilityMode
    )
    {
        var valueSize = compatibilityMode == SlmpCompatibilityMode.Legacy ? 1 : 2;
        var encodedSpecs = new byte[bitEntries.Count][];
        var size = 1 + (bitEntries.Count * valueSize);
        for (var i = 0; i < bitEntries.Count; i++)
        {
            var (device, _, extension) = bitEntries[i];
            var spec = EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension), compatibilityMode);
            encodedSpecs[i] = spec;
            size += spec.Length;
        }

        var payload = new byte[size];
        payload[0] = (byte)bitEntries.Count;
        var offset = 1;
        for (var i = 0; i < bitEntries.Count; i++)
        {
            var spec = encodedSpecs[i];
            spec.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += spec.Length;
            if (compatibilityMode == SlmpCompatibilityMode.Legacy)
            {
                payload[offset++] = bitEntries[i].Value ? (byte)1 : (byte)0;
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), bitEntries[i].Value ? (ushort)1 : (ushort)0);
                offset += 2;
            }
        }

        return payload;
    }

    internal static byte[] BuildExtendedMonitorRegisterPayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices,
        SlmpCompatibilityMode compatibilityMode
    )
    {
        var encodedWords = new byte[wordDevices.Count][];
        var encodedDwords = new byte[dwordDevices.Count][];
        var size = 2;
        for (var i = 0; i < wordDevices.Count; i++)
        {
            var (device, extension) = wordDevices[i];
            var spec = EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension), compatibilityMode);
            encodedWords[i] = spec;
            size += spec.Length;
        }

        for (var i = 0; i < dwordDevices.Count; i++)
        {
            var (device, extension) = dwordDevices[i];
            var spec = EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension), compatibilityMode);
            encodedDwords[i] = spec;
            size += spec.Length;
        }

        var payload = new byte[size];
        payload[0] = (byte)wordDevices.Count;
        payload[1] = (byte)dwordDevices.Count;
        var offset = 2;
        foreach (var spec in encodedWords)
        {
            spec.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += spec.Length;
        }
        foreach (var spec in encodedDwords)
        {
            spec.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += spec.Length;
        }
        return payload;
    }

    internal static byte[] EncodeExtendedDeviceSpec(
        SlmpDeviceAddress device,
        SlmpExtensionSpec extension,
        SlmpCompatibilityMode compatibilityMode)
    {
        if (extension.DirectMemorySpecification == 0xF9)
            return EncodeLinkDirectDeviceSpec(device, extension);

        var captureAligned = (device.Code is SlmpDeviceCode.G or SlmpDeviceCode.HG) && (extension.DirectMemorySpecification is 0xF8 or 0xFA);
        var deviceSpec = new byte[DeviceSpecSize(compatibilityMode)];
        _ = EncodeDeviceSpec(device, deviceSpec, compatibilityMode);
        if (captureAligned)
        {
            var payload = new byte[2 + deviceSpec.Length + 1 + 1 + 2 + 1];
            var offset = 0;
            payload[offset++] = extension.ExtensionSpecificationModification;
            payload[offset++] = extension.DeviceModificationIndex;
            deviceSpec.CopyTo(payload, offset);
            offset += deviceSpec.Length;
            payload[offset++] = extension.DeviceModificationFlags;
            payload[offset++] = 0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), extension.ExtensionSpecification);
            offset += 2;
            payload[offset] = extension.DirectMemorySpecification;
            return payload;
        }

        var data = new byte[2 + 1 + 1 + 1 + deviceSpec.Length + 1];
        var cursor = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(cursor, 2), extension.ExtensionSpecification);
        cursor += 2;
        data[cursor++] = extension.ExtensionSpecificationModification;
        data[cursor++] = extension.DeviceModificationIndex;
        data[cursor++] = extension.DeviceModificationFlags;
        deviceSpec.CopyTo(data, cursor);
        cursor += deviceSpec.Length;
        data[cursor] = extension.DirectMemorySpecification;
        return data;
    }

    internal static byte[] BuildLabelArrayReadPayload(IReadOnlyList<SlmpLabelArrayReadPoint> points, IReadOnlyList<string> abbreviationLabels)
    {
        var size = 4;
        foreach (var name in abbreviationLabels)
            size += GetEncodedLabelNameSize(name);
        foreach (var pt in points)
            size += GetEncodedLabelNameSize(pt.Label) + 4;

        var payload = new byte[size];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), checked((ushort)points.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), checked((ushort)abbreviationLabels.Count));
        var offset = 4;
        foreach (var name in abbreviationLabels)
            offset += WriteLabelName(payload.AsSpan(offset), name);
        foreach (var pt in points)
        {
            offset += WriteLabelName(payload.AsSpan(offset), pt.Label);
            payload[offset++] = pt.UnitSpecification;
            payload[offset++] = 0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), pt.ArrayDataLength);
            offset += 2;
        }

        return payload;
    }

    internal static byte[] BuildLabelArrayWritePayload(IReadOnlyList<SlmpLabelArrayWritePoint> points, IReadOnlyList<string> abbreviationLabels)
    {
        var size = 4;
        foreach (var name in abbreviationLabels)
            size += GetEncodedLabelNameSize(name);
        foreach (var pt in points)
            size += GetEncodedLabelNameSize(pt.Label) + 4 + pt.Data.Length;

        var payload = new byte[size];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), checked((ushort)points.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), checked((ushort)abbreviationLabels.Count));
        var offset = 4;
        foreach (var name in abbreviationLabels)
            offset += WriteLabelName(payload.AsSpan(offset), name);
        foreach (var pt in points)
        {
            offset += WriteLabelName(payload.AsSpan(offset), pt.Label);
            payload[offset++] = pt.UnitSpecification;
            payload[offset++] = 0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), pt.ArrayDataLength);
            offset += 2;
            pt.Data.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += pt.Data.Length;
        }

        return payload;
    }

    internal static byte[] BuildLabelRandomReadPayload(IReadOnlyList<string> labels, IReadOnlyList<string> abbreviationLabels)
    {
        var size = 4;
        foreach (var name in abbreviationLabels)
            size += GetEncodedLabelNameSize(name);
        foreach (var label in labels)
            size += GetEncodedLabelNameSize(label);

        var payload = new byte[size];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), checked((ushort)labels.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), checked((ushort)abbreviationLabels.Count));
        var offset = 4;
        foreach (var name in abbreviationLabels)
            offset += WriteLabelName(payload.AsSpan(offset), name);
        foreach (var label in labels)
            offset += WriteLabelName(payload.AsSpan(offset), label);
        return payload;
    }

    internal static byte[] BuildLabelRandomWritePayload(IReadOnlyList<SlmpLabelRandomWritePoint> points, IReadOnlyList<string> abbreviationLabels)
    {
        var size = 4;
        foreach (var name in abbreviationLabels)
            size += GetEncodedLabelNameSize(name);
        foreach (var pt in points)
            size += GetEncodedLabelNameSize(pt.Label) + 2 + pt.Data.Length;

        var payload = new byte[size];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), checked((ushort)points.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), checked((ushort)abbreviationLabels.Count));
        var offset = 4;
        foreach (var name in abbreviationLabels)
            offset += WriteLabelName(payload.AsSpan(offset), name);
        foreach (var pt in points)
        {
            offset += WriteLabelName(payload.AsSpan(offset), pt.Label);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), checked((ushort)pt.Data.Length));
            offset += 2;
            pt.Data.AsSpan().CopyTo(payload.AsSpan(offset));
            offset += pt.Data.Length;
        }

        return payload;
    }

    internal static SlmpLabelArrayReadResult[] ParseArrayLabelReadResponse(byte[] data)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var results = new SlmpLabelArrayReadResult[count];
        var offset = 2;
        for (var i = 0; i < count; i++)
        {
            var dtId = data[offset];
            var uSpec = data[offset + 1];
            var aLen = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2, 2));
            offset += 4;
            var dataSize = uSpec == 0 ? aLen * 2 : aLen;
            results[i] = new SlmpLabelArrayReadResult(dtId, uSpec, aLen, data[offset..(offset + dataSize)]);
            offset += dataSize;
        }
        return results;
    }

    internal static SlmpLabelRandomReadResult[] ParseRandomLabelReadResponse(byte[] data)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var results = new SlmpLabelRandomReadResult[count];
        var offset = 2;
        for (var i = 0; i < count; i++)
        {
            var dtId = data[offset];
            var spare = data[offset + 1];
            var rLen = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2, 2));
            offset += 4;
            results[i] = new SlmpLabelRandomReadResult(dtId, spare, rLen, data[offset..(offset + rLen)]);
            offset += rLen;
        }
        return results;
    }

    private static int GetEncodedLabelNameSize(string label)
    {
        if (string.IsNullOrEmpty(label))
            throw new ArgumentException("Label name must not be empty.", nameof(label));
        return 2 + Encoding.Unicode.GetByteCount(label);
    }

    private static int WriteLabelName(Span<byte> buffer, string label)
    {
        if (string.IsNullOrEmpty(label))
            throw new ArgumentException("Label name must not be empty.", nameof(label));
        var byteCount = Encoding.Unicode.GetByteCount(label);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[..2], checked((ushort)(byteCount / 2)));
        _ = Encoding.Unicode.GetBytes(label.AsSpan(), buffer.Slice(2, byteCount));
        return 2 + byteCount;
    }

    private static byte[] EncodeLinkDirectDeviceSpec(SlmpDeviceAddress device, SlmpExtensionSpec extension)
    {
        // Format verified by GOT pcap (J2\SW10):
        // reserved(2) + dev_no(3 LE) + dev_code(1) + reserved(2) + j_net(1) + reserved(1) + 0xF9
        var jNet = (byte)(extension.ExtensionSpecification & 0xFF);
        var devCode = (byte)((ushort)device.Code & 0xFF);
        return
        [
            0x00, 0x00,
            (byte)(device.Number & 0xFF), (byte)((device.Number >> 8) & 0xFF), (byte)((device.Number >> 16) & 0xFF),
            devCode,
            0x00, 0x00,
            jNet,
            0x00,
            0xF9,
        ];
    }
}
