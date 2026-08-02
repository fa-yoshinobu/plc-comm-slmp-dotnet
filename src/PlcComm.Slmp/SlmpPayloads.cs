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
        => EncodeRawDeviceSpec(new SlmpRawDeviceAddress(device.Code, device.Number), output, compatibilityMode);

    internal static int EncodeRawDeviceSpec(
        SlmpRawDeviceAddress device,
        Span<byte> output,
        SlmpCompatibilityMode compatibilityMode)
    {
        // PROFILE_RANGE_NOT_A_TRANSPORT_GUARD: PLC profile device ranges are
        // application metadata. Transport validation is limited to syntax,
        // supported device families, and the selected wire representation.
        if (!Enum.IsDefined(device.Code))
            throw new ArgumentOutOfRangeException(nameof(device), device.Code, "Undefined SLMP device codes cannot be encoded.");
        if (compatibilityMode == SlmpCompatibilityMode.Legacy)
        {
            if (device.Number > 0x00FF_FFFF)
                throw new ArgumentOutOfRangeException(
                    nameof(device),
                    device.Number,
                    "Legacy device numbers must fit the 24-bit wire field (0..16777215).");
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

    internal static SlmpExtensionSpec ResolveEffectiveExtension(
        SlmpQualifiedDeviceAddress device,
        SlmpPlcProfile plcProfile)
    {
        var result = new SlmpExtensionSpec(
            device.ExtensionSpecification ?? 0,
            0,
            0,
            0,
            device.DirectMemorySpecification ?? 0);
        if (device.DirectMemorySpecification == 0xF9 && device.Modification is not null)
            throw new ArgumentException("J-qualified link-direct devices do not support Z, LZ, or indirect modification.", nameof(device));
        result = device.Modification switch
        {
            null => result,
            SlmpDeviceModification.IndexZ index => result with
            {
                DeviceModificationIndex = index.Index,
                DeviceModificationFlags = 0x40,
            },
            SlmpDeviceModification.IndexLz index when SlmpPlcProfiles.UsesIqrProtocol(plcProfile) => result with
            {
                DeviceModificationIndex = index.Index,
                DeviceModificationFlags = 0x80,
            },
            SlmpDeviceModification.IndexLz => throw new NotSupportedException(
                $"LZ index modification is not supported for PlcProfile '{SlmpPlcProfiles.ToCanonicalString(plcProfile)}'."),
            SlmpDeviceModification.Indirect => result with { DeviceModificationFlags = 0x08 },
            _ => throw new ArgumentOutOfRangeException(nameof(device)),
        };

        switch (device.Device.Code)
        {
            case SlmpDeviceCode.G:
                if (device.ExtensionSpecification is null)
                    throw new ArgumentException(@"G Extended Device access requires U-qualified module access such as U1\G0.", nameof(device));
                result = RequireDirectMemory(result, 0xF8, "G");
                break;
            case SlmpDeviceCode.HG:
                if (device.ExtensionSpecification is null)
                    throw new ArgumentException(@"HG Extended Device access requires U-qualified CPU-buffer access U3E0\HG through U3E3\HG.", nameof(device));
                if (!IsValidHgExtensionSpecification(device.ExtensionSpecification.Value))
                    throw new ArgumentException(@"HG Extended Device access is valid only for U3E0\HG through U3E3\HG.", nameof(device));
                result = RequireDirectMemory(result, 0xFA, "HG");
                break;
        }

        return result;
    }

    private static SlmpExtensionSpec RequireDirectMemory(SlmpExtensionSpec extension, byte requiredDirectMemory, string deviceCode)
    {
        if (extension.DirectMemorySpecification == 0x00)
            return extension with { DirectMemorySpecification = requiredDirectMemory };
        if (extension.DirectMemorySpecification != requiredDirectMemory)
            throw new ArgumentException(
                $"{deviceCode} Extended Device access requires DirectMemorySpecification=0x{requiredDirectMemory:X2}; got 0x{extension.DirectMemorySpecification:X2}.",
                nameof(extension));
        return extension;
    }

    private static bool IsValidHgExtensionSpecification(ushort extensionSpecification)
        => extensionSpecification is >= 0x03E0 and <= 0x03E3;

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
        IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices,
        IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices,
        SlmpCompatibilityMode compatibilityMode,
        SlmpPlcProfile plcProfile
    )
    {
        var wordExtensions = new SlmpExtensionSpec[wordDevices.Count];
        var dwordExtensions = new SlmpExtensionSpec[dwordDevices.Count];
        var size = 2;
        for (var i = 0; i < wordDevices.Count; i++)
        {
            var device = wordDevices[i];
            var extension = ResolveEffectiveExtension(device, plcProfile);
            wordExtensions[i] = extension;
            size = checked(size + ValidateAndGetExtendedDeviceSpecSize(device.Device, extension, compatibilityMode));
        }

        for (var i = 0; i < dwordDevices.Count; i++)
        {
            var device = dwordDevices[i];
            var extension = ResolveEffectiveExtension(device, plcProfile);
            dwordExtensions[i] = extension;
            size = checked(size + ValidateAndGetExtendedDeviceSpecSize(device.Device, extension, compatibilityMode));
        }

        var payload = new byte[size];
        SlmpPerformanceDiagnostics.Report("extended-final-payload");
        payload[0] = (byte)wordDevices.Count;
        payload[1] = (byte)dwordDevices.Count;
        var offset = 2;
        for (var i = 0; i < wordDevices.Count; i++)
        {
            offset += EncodeExtendedDeviceSpecInto(
                wordDevices[i].Device,
                wordExtensions[i],
                compatibilityMode,
                payload.AsSpan(offset));
        }
        for (var i = 0; i < dwordDevices.Count; i++)
        {
            offset += EncodeExtendedDeviceSpecInto(
                dwordDevices[i].Device,
                dwordExtensions[i],
                compatibilityMode,
                payload.AsSpan(offset));
        }
        return payload;
    }

    internal static byte[] BuildExtendedRandomWordWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value)> dwordEntries,
        SlmpCompatibilityMode compatibilityMode,
        SlmpPlcProfile plcProfile
    )
    {
        var wordExtensions = new SlmpExtensionSpec[wordEntries.Count];
        var dwordExtensions = new SlmpExtensionSpec[dwordEntries.Count];
        var size = checked(2 + checked(wordEntries.Count * 2) + checked(dwordEntries.Count * 4));
        for (var i = 0; i < wordEntries.Count; i++)
        {
            var (device, _) = wordEntries[i];
            var extension = ResolveEffectiveExtension(device, plcProfile);
            wordExtensions[i] = extension;
            size = checked(size + ValidateAndGetExtendedDeviceSpecSize(device.Device, extension, compatibilityMode));
        }

        for (var i = 0; i < dwordEntries.Count; i++)
        {
            var (device, _) = dwordEntries[i];
            var extension = ResolveEffectiveExtension(device, plcProfile);
            dwordExtensions[i] = extension;
            size = checked(size + ValidateAndGetExtendedDeviceSpecSize(device.Device, extension, compatibilityMode));
        }

        var payload = new byte[size];
        SlmpPerformanceDiagnostics.Report("extended-final-payload");
        payload[0] = (byte)wordEntries.Count;
        payload[1] = (byte)dwordEntries.Count;
        var offset = 2;
        for (var i = 0; i < wordEntries.Count; i++)
        {
            offset += EncodeExtendedDeviceSpecInto(
                wordEntries[i].Device.Device,
                wordExtensions[i],
                compatibilityMode,
                payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), wordEntries[i].Value);
            offset += 2;
        }

        for (var i = 0; i < dwordEntries.Count; i++)
        {
            offset += EncodeExtendedDeviceSpecInto(
                dwordEntries[i].Device.Device,
                dwordExtensions[i],
                compatibilityMode,
                payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), dwordEntries[i].Value);
            offset += 4;
        }

        return payload;
    }

    internal static byte[] BuildExtendedRandomBitWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, bool Value)> bitEntries,
        SlmpCompatibilityMode compatibilityMode,
        SlmpPlcProfile plcProfile
    )
    {
        var extensions = new SlmpExtensionSpec[bitEntries.Count];
        var qlValueEncoding = new bool[bitEntries.Count];
        var size = 1;
        for (var i = 0; i < bitEntries.Count; i++)
        {
            var (device, _) = bitEntries[i];
            var extension = ResolveEffectiveExtension(device, plcProfile);
            extensions[i] = extension;
            qlValueEncoding[i] = compatibilityMode == SlmpCompatibilityMode.Legacy ||
                extension.DirectMemorySpecification == 0xF9;
            size = checked(size + ValidateAndGetExtendedDeviceSpecSize(device.Device, extension, compatibilityMode) + (qlValueEncoding[i] ? 1 : 2));
        }

        var payload = new byte[size];
        SlmpPerformanceDiagnostics.Report("extended-final-payload");
        payload[0] = (byte)bitEntries.Count;
        var offset = 1;
        for (var i = 0; i < bitEntries.Count; i++)
        {
            offset += EncodeExtendedDeviceSpecInto(
                bitEntries[i].Device.Device,
                extensions[i],
                compatibilityMode,
                payload.AsSpan(offset));
            if (qlValueEncoding[i])
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
        IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices,
        IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices,
        SlmpCompatibilityMode compatibilityMode,
        SlmpPlcProfile plcProfile
    )
    {
        var wordExtensions = new SlmpExtensionSpec[wordDevices.Count];
        var dwordExtensions = new SlmpExtensionSpec[dwordDevices.Count];
        var size = 2;
        for (var i = 0; i < wordDevices.Count; i++)
        {
            var device = wordDevices[i];
            var extension = ResolveEffectiveExtension(device, plcProfile);
            wordExtensions[i] = extension;
            size = checked(size + ValidateAndGetExtendedDeviceSpecSize(device.Device, extension, compatibilityMode));
        }

        for (var i = 0; i < dwordDevices.Count; i++)
        {
            var device = dwordDevices[i];
            var extension = ResolveEffectiveExtension(device, plcProfile);
            dwordExtensions[i] = extension;
            size = checked(size + ValidateAndGetExtendedDeviceSpecSize(device.Device, extension, compatibilityMode));
        }

        var payload = new byte[size];
        SlmpPerformanceDiagnostics.Report("extended-final-payload");
        payload[0] = (byte)wordDevices.Count;
        payload[1] = (byte)dwordDevices.Count;
        var offset = 2;
        for (var i = 0; i < wordDevices.Count; i++)
        {
            offset += EncodeExtendedDeviceSpecInto(
                wordDevices[i].Device,
                wordExtensions[i],
                compatibilityMode,
                payload.AsSpan(offset));
        }
        for (var i = 0; i < dwordDevices.Count; i++)
        {
            offset += EncodeExtendedDeviceSpecInto(
                dwordDevices[i].Device,
                dwordExtensions[i],
                compatibilityMode,
                payload.AsSpan(offset));
        }
        return payload;
    }

    internal static byte[] EncodeExtendedDeviceSpec(
        SlmpDeviceAddress device,
        SlmpExtensionSpec extension,
        SlmpCompatibilityMode compatibilityMode)
    {
        var data = new byte[ValidateAndGetExtendedDeviceSpecSize(device, extension, compatibilityMode)];
        _ = EncodeExtendedDeviceSpecInto(device, extension, compatibilityMode, data);
        SlmpPerformanceDiagnostics.Report("extended-owned-spec");
        return data;
    }

    private static int ValidateAndGetExtendedDeviceSpecSize(
        SlmpDeviceAddress device,
        SlmpExtensionSpec extension,
        SlmpCompatibilityMode compatibilityMode)
    {
        if (!Enum.IsDefined(device.Code))
            throw new ArgumentOutOfRangeException(nameof(device), device.Code, "Undefined SLMP device codes cannot be encoded.");
        if (extension.DirectMemorySpecification == 0xF9)
        {
            if (device.Number > 0x00FF_FFFF)
                throw new ArgumentOutOfRangeException(
                    nameof(device),
                    device.Number,
                    "Link-direct device numbers must fit the 24-bit Q/L wire field (0..16777215).");
            if (extension.ExtensionSpecification > byte.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(extension),
                    extension.ExtensionSpecification,
                    "Link-direct network numbers must fit the 8-bit wire field (0..255).");
            return 11;
        }

        if (compatibilityMode == SlmpCompatibilityMode.Legacy && device.Number > 0x00FF_FFFF)
            throw new ArgumentOutOfRangeException(
                nameof(device),
                device.Number,
                "Legacy device numbers must fit the 24-bit wire field (0..16777215).");
        return DeviceSpecSize(compatibilityMode) + 7;
    }

    private static int EncodeExtendedDeviceSpecInto(
        SlmpDeviceAddress device,
        SlmpExtensionSpec extension,
        SlmpCompatibilityMode compatibilityMode,
        Span<byte> output)
    {
        if (extension.DirectMemorySpecification == 0xF9)
        {
            output[0] = 0;
            output[1] = 0;
            output[2] = (byte)(device.Number & 0xFF);
            output[3] = (byte)((device.Number >> 8) & 0xFF);
            output[4] = (byte)((device.Number >> 16) & 0xFF);
            output[5] = (byte)((ushort)device.Code & 0xFF);
            output[6] = 0;
            output[7] = 0;
            output[8] = (byte)extension.ExtensionSpecification;
            output[9] = 0;
            output[10] = 0xF9;
            return 11;
        }

        var offset = 0;
        output[offset++] = extension.DeviceModificationIndex;
        output[offset++] = extension.DeviceModificationFlags;
        if (compatibilityMode == SlmpCompatibilityMode.Legacy)
        {
            output[offset++] = (byte)(device.Number & 0xFF);
            output[offset++] = (byte)((device.Number >> 8) & 0xFF);
            output[offset++] = (byte)((device.Number >> 16) & 0xFF);
            output[offset++] = (byte)((ushort)device.Code & 0xFF);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(offset, 4), device.Number);
            offset += 4;
            BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(offset, 2), (ushort)device.Code);
            offset += 2;
        }
        output[offset++] = extension.ExtensionSpecificationModification;
        output[offset++] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(offset, 2), extension.ExtensionSpecification);
        offset += 2;
        output[offset++] = extension.DirectMemorySpecification;
        return offset;
    }

    internal static byte[] BuildLabelArrayReadPayload(IReadOnlyList<SlmpLabelArrayReadPoint> points, IReadOnlyList<string> abbreviationLabels)
    {
        ValidateLabelCounts(points, abbreviationLabels);
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point is null)
                throw new ArgumentException($"The point collection contains null at index {index}.", nameof(points));
            ValidateAbbreviationReferences(point.Label, abbreviationLabels.Count, nameof(points));
            _ = GetArrayWireByteCount(point.UnitSpecification, point.ArrayDataLength, nameof(points));
        }
        var size = 4;
        foreach (var name in abbreviationLabels)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                GetEncodedLabelNameSize(name),
                nameof(abbreviationLabels));
        foreach (var pt in points)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                (long)GetEncodedLabelNameSize(pt.Label) + 4,
                nameof(points));

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
        ValidateLabelCounts(points, abbreviationLabels);
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point is null)
                throw new ArgumentException($"The point collection contains null at index {index}.", nameof(points));
            ValidateAbbreviationReferences(point.Label, abbreviationLabels.Count, nameof(points));
            if (point.Data is null)
                throw new ArgumentException($"Point.Data must not be null at index {index}.", nameof(points));
            var expectedDataLength = GetArrayWireByteCount(point.UnitSpecification, point.ArrayDataLength, nameof(points));
            if (point.Data.Length != expectedDataLength)
            {
                throw new ArgumentException(
                    $"Array label data length mismatch at index {index}: expected={expectedDataLength}, actual={point.Data.Length}.",
                    nameof(points));
            }
        }
        var size = 4;
        foreach (var name in abbreviationLabels)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                GetEncodedLabelNameSize(name),
                nameof(abbreviationLabels));
        foreach (var pt in points)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                (long)GetEncodedLabelNameSize(pt.Label) + 4 + pt.Data.Length,
                nameof(points));

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
        ValidateLabelCounts(labels, abbreviationLabels);
        foreach (var label in labels)
            ValidateAbbreviationReferences(label, abbreviationLabels.Count, nameof(labels));
        var size = 4;
        foreach (var name in abbreviationLabels)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                GetEncodedLabelNameSize(name),
                nameof(abbreviationLabels));
        foreach (var label in labels)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                GetEncodedLabelNameSize(label),
                nameof(labels));

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
        ValidateLabelCounts(points, abbreviationLabels);
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point is null)
                throw new ArgumentException($"The point collection contains null at index {index}.", nameof(points));
            ValidateAbbreviationReferences(point.Label, abbreviationLabels.Count, nameof(points));
            if (point.Data is null)
                throw new ArgumentException($"Point.Data must not be null at index {index}.", nameof(points));
            if (point.Data.Length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(points),
                    point.Data.Length,
                    $"Random label write data length actual={point.Data.Length} exceeds maximum={ushort.MaxValue} bytes at index {index}.");
            }
            if (point.Data.Length == 0 || (point.Data.Length & 1) != 0)
            {
                throw new ArgumentException(
                    $"Random label write data length must be a positive multiple of two bytes at index {index}; actual={point.Data.Length}.",
                    nameof(points));
            }
        }
        var size = 4;
        foreach (var name in abbreviationLabels)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                GetEncodedLabelNameSize(name),
                nameof(abbreviationLabels));
        foreach (var pt in points)
            size = SlmpValidation.AddRequestPayloadLength(
                size,
                (long)GetEncodedLabelNameSize(pt.Label) + 2 + pt.Data.Length,
                nameof(points));

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

    internal static SlmpLabelArrayReadResult[] ParseArrayLabelReadResponse(
        byte[] data,
        IReadOnlyList<SlmpLabelArrayReadPoint> requestedPoints)
    {
        ArgumentNullException.ThrowIfNull(data);
        return ParseArrayLabelReadResponse((ReadOnlyMemory<byte>)data, requestedPoints);
    }

    internal static SlmpLabelArrayReadResult[] ParseArrayLabelReadResponse(
        ReadOnlyMemory<byte> data,
        IReadOnlyList<SlmpLabelArrayReadPoint> requestedPoints)
    {
        ArgumentNullException.ThrowIfNull(requestedPoints);
        var reader = new PayloadReader(data.Span, "array-label response");
        var count = reader.ReadUInt16LittleEndian("number of array points");
        if (count != requestedPoints.Count)
        {
            throw new SlmpError(
                $"Malformed array-label response: response count={count}, requested count={requestedPoints.Count}.");
        }

        var results = new SlmpLabelArrayReadResult[count];
        for (var i = 0; i < count; i++)
        {
            var dtId = reader.ReadByte($"item {i + 1} data type ID");
            var uSpec = reader.ReadByte($"item {i + 1} read unit specification");
            var aLen = reader.ReadUInt16LittleEndian($"item {i + 1} read array data length");
            if (uSpec is not 0 and not 1)
            {
                throw new SlmpError(
                    $"Malformed array-label response: item={i + 1}, unit specification={uSpec}; expected 0 or 1.");
            }
            if (aLen == 0)
            {
                throw new SlmpError($"Malformed array-label response: item={i + 1}, array data length must be greater than zero.");
            }

            var requested = requestedPoints[i];
            if (requested is null)
                throw new ArgumentException($"The requested point collection contains null at index {i}.", nameof(requestedPoints));
            if (uSpec != requested.UnitSpecification || aLen != requested.ArrayDataLength)
            {
                throw new SlmpError(
                    $"Malformed array-label response: item={i + 1}, response unit/length={uSpec}/{aLen}, requested={requested.UnitSpecification}/{requested.ArrayDataLength}.");
            }

            var dataSize = GetArrayWireByteCount(uSpec, aLen, nameof(data));
            results[i] = new SlmpLabelArrayReadResult(
                dtId,
                uSpec,
                aLen,
                reader.ReadBytes(dataSize, $"item {i + 1} read data").ToArray());
        }

        reader.EnsureFullyConsumed();
        return results;
    }

    internal static SlmpLabelRandomReadResult[] ParseRandomLabelReadResponse(byte[] data, int expectedCount)
    {
        ArgumentNullException.ThrowIfNull(data);
        return ParseRandomLabelReadResponse((ReadOnlyMemory<byte>)data, expectedCount);
    }

    internal static SlmpLabelRandomReadResult[] ParseRandomLabelReadResponse(
        ReadOnlyMemory<byte> data,
        int expectedCount)
    {
        if (expectedCount is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(expectedCount));
        var reader = new PayloadReader(data.Span, "random-label response");
        var count = reader.ReadUInt16LittleEndian("number of read data points");
        if (count != expectedCount)
        {
            throw new SlmpError(
                $"Malformed random-label response: response count={count}, requested count={expectedCount}.");
        }

        var results = new SlmpLabelRandomReadResult[count];
        for (var i = 0; i < count; i++)
        {
            var dtId = reader.ReadByte($"item {i + 1} data type ID");
            var spare = reader.ReadByte($"item {i + 1} spare data");
            var rLen = reader.ReadUInt16LittleEndian($"item {i + 1} read data length");
            if (rLen == 0 || (rLen & 1) != 0)
            {
                throw new SlmpError(
                    $"Malformed random-label response: item={i + 1}, read data length={rLen}; expected a positive multiple of two bytes.");
            }
            results[i] = new SlmpLabelRandomReadResult(
                dtId,
                spare,
                rLen,
                reader.ReadBytes(rLen, $"item {i + 1} read data").ToArray());
        }

        reader.EnsureFullyConsumed();
        return results;
    }

    internal static int GetArrayWireByteCount(byte unitSpecification, ushort arrayDataLength, string parameterName)
    {
        if (arrayDataLength == 0)
            throw new ArgumentOutOfRangeException(parameterName, "Array data length must be greater than zero.");
        return unitSpecification switch
        {
            0 => (((int)arrayDataLength + 15) / 16) * 2,
            1 => (((int)arrayDataLength + 1) / 2) * 2,
            _ => throw new ArgumentOutOfRangeException(
                parameterName,
                "UnitSpecification must be 0 (bit) or 1 (byte)."),
        };
    }

    private static int GetEncodedLabelNameSize(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label name must not be empty.", nameof(label));
        var byteCount = Encoding.Unicode.GetByteCount(label);
        if (byteCount / 2 > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(label), "Label name is too long.");
        return 2 + byteCount;
    }

    private static int WriteLabelName(Span<byte> buffer, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label name must not be empty.", nameof(label));
        var byteCount = Encoding.Unicode.GetByteCount(label);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[..2], checked((ushort)(byteCount / 2)));
        _ = Encoding.Unicode.GetBytes(label.AsSpan(), buffer.Slice(2, byteCount));
        return 2 + byteCount;
    }

    private static void ValidateLabelCounts<T>(IReadOnlyList<T> points, IReadOnlyList<string> abbreviationLabels)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(abbreviationLabels);
        if (points.Count is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(points), "Label point count must be in the range 1..65535.");
        if (abbreviationLabels.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(abbreviationLabels), "Abbreviation label count must be at most 65535.");
        for (var index = 0; index < abbreviationLabels.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(abbreviationLabels[index]))
                throw new ArgumentException($"Abbreviation label at index {index} must not be empty.", nameof(abbreviationLabels));
            _ = GetEncodedLabelNameSize(abbreviationLabels[index]);
        }
    }

    private static void ValidateAbbreviationReferences(string label, int abbreviationCount, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label name must not be empty.", parameterName);
        _ = GetEncodedLabelNameSize(label);
        for (var index = 0; index < label.Length; index++)
        {
            if (label[index] != '%')
                continue;
            var digitStart = index + 1;
            var digitEnd = digitStart;
            while (digitEnd < label.Length && char.IsAsciiDigit(label[digitEnd]))
                digitEnd++;
            if (digitEnd == digitStart ||
                !int.TryParse(label.AsSpan(digitStart, digitEnd - digitStart), out var reference) ||
                reference < 1 || reference > abbreviationCount)
            {
                throw new ArgumentException(
                    $"Label '{label}' contains an invalid abbreviation reference; use %1 through %{abbreviationCount}.",
                    parameterName);
            }
            index = digitEnd - 1;
        }
    }

    private ref struct PayloadReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private readonly string _context;
        private int _offset;

        internal PayloadReader(ReadOnlySpan<byte> data, string context)
        {
            _data = data;
            _context = context;
            _offset = 0;
        }

        internal byte ReadByte(string field)
            => ReadBytes(1, field)[0];

        internal ushort ReadUInt16LittleEndian(string field)
            => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2, field));

        internal ReadOnlySpan<byte> ReadBytes(int count, string field)
        {
            var remaining = _data.Length - _offset;
            if (count < 0 || remaining < count)
            {
                throw new SlmpError(
                    $"Malformed {_context}: field={field}, required={count}, remaining={remaining}.");
            }

            var result = _data.Slice(_offset, count);
            _offset += count;
            return result;
        }

        internal void EnsureFullyConsumed()
        {
            var remaining = _data.Length - _offset;
            if (remaining != 0)
                throw new SlmpError($"Malformed {_context}: trailing bytes={remaining}.");
        }
    }

    private static byte[] EncodeLinkDirectDeviceSpec(SlmpDeviceAddress device, SlmpExtensionSpec extension)
    {
        if (!Enum.IsDefined(device.Code))
            throw new ArgumentOutOfRangeException(nameof(device), device.Code, "Undefined SLMP device codes cannot be encoded.");
        if (device.Number > 0x00FF_FFFF)
            throw new ArgumentOutOfRangeException(
                nameof(device),
                device.Number,
                "Link-direct device numbers must fit the 24-bit Q/L wire field (0..16777215).");
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
