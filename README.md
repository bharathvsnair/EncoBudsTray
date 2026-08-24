# EncoBudsTray

Minimal Windows system-tray battery monitor for **OPPO Enco Buds2**.

It displays only:

- Left earbud battery
- Right earbud battery
- Charging-case battery

The tray menu contains only **Refresh** and **Exit**.

## Requirements

- Windows 10 version 2004 (build 19041) or later
- .NET 8 SDK
- OPPO Enco Buds2 paired/available through Windows Bluetooth
- Bluetooth enabled

## Build

From the project directory:

```powershell
dotnet restore
dotnet build -c Release
```

Run:

```powershell
dotnet run -c Release
```

Publish a self-contained Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published executable is placed under:

```text
bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\
```

## Communication model

The implementation follows the OPPO Enco Buds2 path found in the supplied Gadgetbridge source archive.

Gadgetbridge identifies the device through the exact name:

```text
OPPO Enco Buds2
```

The Gadgetbridge coordinator is an `AbstractBLClassicDeviceCoordinator`, so this implementation uses Bluetooth Classic RFCOMM.

Gadgetbridge's `OppoHeadphonesSupport` declares this supported RFCOMM service:

```text
0000079a-d102-11e1-9b23-00025b00a5a5
```

The Windows application discovers that RFCOMM service and additionally verifies the device name before connecting.

No generic headset selection or manually entered Bluetooth address is used.

## Battery protocol

Only the battery request/response portion of Gadgetbridge is implemented.

### Request

Gadgetbridge's `OppoCommand.BATTERY_REQ` is:

```text
0x0106
```

`OppoHeadphonesProtocol.encodeBatteryReq()` constructs the message using the common Gadgetbridge `encodeMessage()` framing:

```text
AA
length
00 00
06 01
sequence
payload-length (00 00)
```

For an empty battery-request payload, the initial request is:

```text
AA 09 00 00 06 01 00 00 00
```

The sequence byte increments for subsequent requests.

### Response

The battery response command is:

```text
0x8106
```

The parser follows Gadgetbridge's little-endian response framing and battery parsing.

For each battery entry:

```text
protocol index 1 → application index 0 → Left
protocol index 2 → application index 1 → Right
protocol index 3 → application index 2 → Case
```

The battery level is:

```text
raw byte & 0x7F
```

The high bit:

```text
raw byte & 0x80
```

represents the charging state.

Gadgetbridge ignores a case reading when the case level is zero. The Windows implementation preserves that behavior.

Missing/invalid values are represented as `null`, not `0%`.

## Connection and update behavior

The application:

1. Searches for the Enco Buds2 RFCOMM service.
2. Connects using the Windows RFCOMM API.
3. Sends only the battery request.
4. Reads and parses the response.
5. Updates the tray tooltip.
6. Polls again approximately every 30 seconds.
7. Retries the battery request with a timeout.
8. Disconnects and reconnects automatically after communication failure.

Initial discovery uses a short reconnect delay with exponential backoff up to 30 seconds.

A manual **Refresh** wakes the monitor immediately.

No continuous Bluetooth scan is performed while a connection is active.

## Tray display

Connected example:

```text
OPPO Enco Buds2 — L: 70% | R: 90% | Case: 40%
```

Unavailable value:

```text
OPPO Enco Buds2 — L: 70% | R: 90% | Case: —
```

Disconnected:

```text
OPPO Enco Buds2 — Disconnected
```

Searching:

```text
OPPO Enco Buds2 — Searching...
```

## Logging

Lightweight logs are written locally to:

```text
%LOCALAPPDATA%\EncoBudsTray\enco.log
```

Only connection state, errors, and parsed battery values are logged by default.

Raw protocol packets are not logged by default.

## Gadgetbridge source traceability

The supplied Gadgetbridge archive is the sole protocol/device source used for this implementation.

Relevant source locations:

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/devices/oppo/OppoEncoBuds2Coordinator.java
```

Used for:

- Exact supported device name: `OPPO Enco Buds2`
- Device-specific coordinator identity

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/devices/oppo/OppoHeadphonesCoordinator.java
```

Used for:

- Bluetooth Classic coordinator type
- Three battery slots
- Battery index semantics:
  - 0 = left
  - 1 = right
  - 2 = case

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/OppoHeadphonesSupport.java
```

Used for:

- RFCOMM service UUID
- OPPO serial/RFCOMM support path
- Gadgetbridge's battery request behavior
- Gadgetbridge's battery retry behavior

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/OppoHeadphonesProtocol.java
```

Used for:

- OPPO frame structure
- Battery request encoding
- Battery response decoding
- Battery index mapping
- Charging-bit interpretation
- Unknown-value behavior

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/commands/OppoCommand.java
```

Used for:

- `BATTERY_REQ = 0x0106`
- `BATTERY_RET = 0x8106`

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/serial/AbstractSerialDeviceSupportV2.java
```

Used to trace that the OPPO implementation sends protocol bytes through the Bluetooth Classic socket path.

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/btbr/AbstractBTBRDeviceSupport.java
```

Used to trace that the OPPO support is using a Bluetooth Classic RFCOMM service UUID and a primary RFCOMM socket.

Test reference:

```text
app/src/test/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/OppoHeadphonesProtocolTest.java
```

The supplied Gadgetbridge battery-response sample is reproduced as a parser test without copying Gadgetbridge implementation code.

## Tests

Run:

```powershell
dotnet test -c Release
```

The tests cover:

- Gadgetbridge battery-response parsing
- Left/right/case mapping
- Charging-bit parsing
- Gadgetbridge's zero-case behavior
- Battery request construction
- Multiple/partial response framing
- Malformed payload lengths

Bluetooth hardware is not required for parser tests.

## Known limitations

- This project implements only the battery protocol path.
- It does not reproduce Gadgetbridge's unrelated initialization commands.
- It does not implement EQ, listening modes, game mode, touch controls, ANC, firmware operations, audio, media, or other earbud functionality.
- The application requires Windows to expose the Gadgetbridge-supported RFCOMM service to the Windows Bluetooth APIs.
- Actual Bluetooth integration should be validated on a Windows system with the Enco Buds2 because the supplied development environment does not contain the Windows/.NET SDK needed for a hardware build test.
