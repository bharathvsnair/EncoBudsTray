using System.Buffers.Binary;
using EncoBudsTray.Models;

namespace EncoBudsTray.Protocol;

/// <summary>
/// Minimal C# implementation of the battery-related portion of Gadgetbridge's
/// OppoHeadphonesProtocol.
///
/// Reference:
/// gadgetbridge/app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/OppoHeadphonesProtocol.java
/// gadgetbridge/app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/commands/OppoCommand.java
/// </summary>
public sealed class OppoBatteryProtocol
{
    private const byte Preamble = 0xAA;
    private const ushort BatteryRequestCode = 0x0106;
    private const ushort BatteryResponseCode = 0x8106;

    private byte _sequence;

    public byte[] CreateBatteryRequest()
    {
        // Gadgetbridge encodeMessage():
        // aa | length | 00 00 | command (little-endian) | sequence |
        // payload length (little-endian) | payload
        const int payloadLength = 0;
        const int frameLength = 9 + payloadLength;

        var frame = new byte[frameLength];
        frame[0] = Preamble;
        frame[1] = (byte)(frame.Length - 2);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4, 2), BatteryRequestCode);
        frame[6] = _sequence++;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7, 2), payloadLength);
        return frame;
    }

    /// <summary>
    /// Parses zero or more Gadgetbridge OPPO protocol frames from a byte buffer.
    /// Incomplete trailing data is returned for the caller to retain.
    /// </summary>
    public ParseResult ParseResponses(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        BatteryState? state = null;

        while (offset + 2 <= data.Length)
        {
            if (data[offset] != Preamble)
            {
                offset++;
                continue;
            }

            var totalLength = data[offset + 1];
            var frameSize = totalLength + 2;

            if (data.Length - offset < frameSize)
                break;

            var frame = data.Slice(offset, frameSize);
            var parsed = ParseSingleResponse(frame);
            if (parsed is not null)
                state = state is null ? parsed : state.Merge(parsed);

            offset += frameSize;
        }

        return new ParseResult(state, data[offset..].ToArray());
    }

    private static BatteryState? ParseSingleResponse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 9 || frame[0] != Preamble || frame[1] + 2 != frame.Length)
            return null;

        var command = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(4, 2));
        if (command != BatteryResponseCode)
            return null;

        // bytes 2..3: reserved/zero
        // byte 6: sequence
        var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(7, 2));

        if (payloadLength > frame.Length - 9)
            return null;

        var payload = frame.Slice(9, payloadLength);
        if (payload.Length < 2 || payload[0] != 0)
            return null;

        // Gadgetbridge starts at payload[2], and maps protocol battery index N
        // to application battery index N-1:
        //   0 = left, 1 = right, 2 = case.
        int? left = null, right = null, @case = null;
        var leftCharging = false;
        var rightCharging = false;
        var caseCharging = false;

        for (var i = 2; i + 1 < payload.Length; i += 2)
        {
            if (payload[i] == 0xFF)
                continue;

            var batteryIndex = payload[i] - 1;
            if (batteryIndex is < 0 or > 2)
                continue;

            var raw = payload[i + 1];
            var level = raw & 0x7F;
            var charging = (raw & 0x80) != 0;

            // Gadgetbridge deliberately ignores a case reading of 0.
            if (batteryIndex == 2 && level == 0)
                continue;

            switch (batteryIndex)
            {
                case 0:
                    left = level;
                    leftCharging = charging;
                    break;
                case 1:
                    right = level;
                    rightCharging = charging;
                    break;
                case 2:
                    @case = level;
                    caseCharging = charging;
                    break;
            }
        }

        // The Gadgetbridge parser emits UNKNOWN (-1) for every battery index
        // not present in this response. The Windows layer represents that as null.
        return new BatteryState(left, right, @case, leftCharging, rightCharging, caseCharging);
    }
}

public sealed record ParseResult(BatteryState? Battery, byte[] Remaining);
