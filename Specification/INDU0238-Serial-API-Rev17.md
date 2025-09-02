Serial API Specification INDUCTOSENSE WAND V3 SERIAL API SPECIFICATION
Project Code: INDU0238 Revision: 17 Authors: L O'Donnell, N Padoin, M
Thompson Date: 15/04/2025

Form: BF010 Version: 3 Date: 12/12/2019

Page 1 of 109

Serial API Specification

Contents Contents
.....................................................................................................................................2
History
........................................................................................................................................6
References
..................................................................................................................................8
Glossary of
terms........................................................................................................................8
1 Introduction
........................................................................................................................9
2 Proposed Framing
Changes.................................................................................................9
2.1
Frame.......................................................................................................................
10 3 New Commands
...............................................................................................................
11 3.1 Processor Firmware Upgrade
..................................................................................
11 Firmware Upload Start
........................................................................................
12 Firmware Block Upload
.......................................................................................
13 Start Firmware Upgrade
......................................................................................
14 Query Firmware Upgrade Status
.........................................................................
15
CRC32...................................................................................................................
16 Get Bootloader Info
.............................................................................................
17 Boot Logo Upload Start
.......................................................................................
18 Boot Logo Block
Upload.......................................................................................
19 Start Boot Logo Update
.......................................................................................
20 Query Boot Logo Update Status
......................................................................
21 3.2
RFID..........................................................................................................................
22 Get RFID Read Tx Power
......................................................................................
22 Set RFID Read Tx
Power.......................................................................................
22 Get RFID
Region...................................................................................................
23 Set RFID
Region....................................................................................................
24 Set RFID Hop Table
..............................................................................................
25 Get RFID Software Version
..................................................................................
26 Get RFID Hardware
Version.................................................................................
27 Get RFID Serial
Number.......................................................................................
28 Get RFID Model
...................................................................................................
29 RFID Firmware Upload Start
............................................................................
30 RFID Firmware Upload Block
...........................................................................
31 Start RFID Firmware
Upgrade..........................................................................
32 Query RFID Firmware Upgrade
Status............................................................. 33
Get RFID Hop
Table..........................................................................................
34 Get RFID Ramp Start Read Tx Power
............................................................... 35 Set
RFID Ramp Start Read Tx
Power................................................................ 35
Get RFID Per-Ramp Read Tx Power
................................................................. 36 Set
RFID Per-Ramp Read Tx
Power..................................................................
36 3.3 Dynamic
Tables........................................................................................................
37 Get First Sensor
Location.....................................................................................
37 Get Next Sensor Location
....................................................................................
38 Add Sensor Location
............................................................................................
39 Get Sensor Location At Index
..............................................................................
40 Replace Sensor
Location......................................................................................
41 Delete All Sensors
................................................................................................
42 Get First Chirp (Wand v3)
....................................................................................
43 Get Next Chirp (Wand v3)
...................................................................................
44

Form: BF010 Version: 3 Date: 12/12/2019

Page 2 of 109

Serial API Specification Add Chirp (Wand v3)
...........................................................................................
45 Clear Chirp Table (Wand
v3)............................................................................
45 Get Chirp (Wand v3) At
Index..........................................................................
46 Replace Chirp (Wand v3) At
Index...................................................................
47 Get First
Material.............................................................................................
48 Get Next Material
............................................................................................
49 Add Material
....................................................................................................
50 Clear Material Table
........................................................................................
50 Get Material At Index
......................................................................................
51 Replace
Material..............................................................................................
52 Get First Sensor
Type.......................................................................................
53 Get Next Sensor Type
......................................................................................
54 Add Sensor Type
..............................................................................................
55 Clear Sensor Type Table
..................................................................................
56 Get Sensor Type At Index
................................................................................
57 Replace Sensor
Type........................................................................................
58 Get First Cartridge
Type...................................................................................
59 Get Next Cartridge Type
..................................................................................
60 Add Cartridge Type
..........................................................................................
61 Clear Cartridge Type Table
..............................................................................
61 Get Cartridge Type At Index
............................................................................
62 Replace Cartridge
Type....................................................................................
63 3.4
Authentication.........................................................................................................
64 Send Challenge
....................................................................................................
64 Get Challenge Response
......................................................................................
65 Request Challenge
...............................................................................................
65 Send Challenge
Response....................................................................................
66 Level 1 Handshake
...............................................................................................
67 3.4.5.1 Challenge Part
1...........................................................................................
68 3.4.5.2 Challenge Part
2...........................................................................................
69
Encryption............................................................................................................
69 End Session
..........................................................................................................
70 Level 2 Handshake
...............................................................................................
71 3.4.8.1 Level 2 Challenge Part 1
..............................................................................
71 3.4.8.2 Level 2 Challenge Part 2
..............................................................................
72 3.5 Miscellaneous
..........................................................................................................
73 KeepAlive
.............................................................................................................
73 Get Battery
%.......................................................................................................
73 SaveToDiskNow
...................................................................................................
74 Get System Reset
Status......................................................................................
74 Reset Fuel Gauge
.................................................................................................
75 USB
Slowdown.....................................................................................................
75 Get System Logs
..................................................................................................
76 Get Fuel Gauge Temperature
..............................................................................
76 3.6 Bluetooth
.................................................................................................................
77 Get Bluetooth RSSI
..............................................................................................
77 Get Bluetooth Enabled
........................................................................................
77 Set Bluetooth Enabled
.........................................................................................
78 Clear Bluetooth Pairings
......................................................................................
78

Form: BF010 Version: 3 Date: 12/12/2019

Page 3 of 109

Serial API Specification Get Bluetooth Address
........................................................................................
79 3.7 Subscription
.............................................................................................................
80 Add
Subscription..................................................................................................
80 Set Active Subscription
........................................................................................
81 Get Active Subscription
.......................................................................................
81 Delete
Subscriptions............................................................................................
82 Get Subscription At Index
....................................................................................
83 Replace Subscription At Index
.............................................................................
84 Find Subscription By
Guid....................................................................................
85 Delete Subscription At Index
...............................................................................
85 Subscription Delete Status
..................................................................................
86 3.8 User
Management...................................................................................................
87
GetFirstUser.........................................................................................................
87 GetNextUser
........................................................................................................
88 AddUser
...............................................................................................................
89 DeleteUser
...........................................................................................................
89 DeleteAllUsers
.....................................................................................................
90 3.9 From Wand v2
.........................................................................................................
91 Sensor Management
...........................................................................................
91 3.9.1.1 DeleteSensor
...............................................................................................
91
Measurements.....................................................................................................
91 3.9.2.1 GetFirstMeasurement
.................................................................................
91 3.9.2.2
GetNextMeasurement.................................................................................
92 3.9.2.3
DeleteAllMeasurements..............................................................................
92 3.9.2.4 GetNumMeasurements
...............................................................................
93 3.9.2.5
GetMeasurementAtIndex............................................................................
93 3.9.2.6
DeleteAllMeasurementsStatus....................................................................
94 High Temperature
operation...............................................................................
95 3.9.3.1 GetHighTempParam
....................................................................................
95 3.9.3.2 SetHighTempParam
.....................................................................................
95 System Delay
.......................................................................................................
96 3.9.4.1 GetSystemDelay
..........................................................................................
96 3.9.4.2 SetSystemDelay
...........................................................................................
96 Wand
Settings......................................................................................................
97 3.9.5.1
GetShutdownTime.......................................................................................
97 3.9.5.2 SetShutdownTime
.......................................................................................
98 3.9.5.3 GetRfidEnable
..............................................................................................
98 3.9.5.4
SetRfidEnable...............................................................................................
99 3.9.5.5 GetVideoIndex
.............................................................................................
99 3.9.5.6
SetVideoIndex............................................................................................
100 3.9.5.7
GetDateTime..............................................................................................
100 3.9.5.8 SetDateTime
..............................................................................................
101 Engineering
Commands.....................................................................................
102 3.9.6.1 GetRfidPower
............................................................................................
102 3.9.6.2 SetRfidPower
.............................................................................................
102 3.9.6.3 DoScan
.......................................................................................................
103 3.9.6.4 GetCurrentFileHdr
.....................................................................................
103 3.9.6.5 GetCurrentFile
...........................................................................................
104 Misc Commands
................................................................................................
104

Form: BF010 Version: 3 Date: 12/12/2019

Page 4 of 109

Serial API Specification 3.9.7.1 Get Information
.........................................................................................
104 3.10 Provisioning
...........................................................................................................
105 ProvisionDeviceKey
.......................................................................................
105
ProvisionDeviceSerialNo................................................................................
105 Remove Provisioning
.....................................................................................
106 3.11 Production Test
.....................................................................................................
107 Start Production
Test.....................................................................................
107 Production Test Status
..................................................................................
108 Program Cartridge EEPROM
..........................................................................
109

Form: BF010 Version: 3 Date: 12/12/2019

Page 5 of 109

Serial API Specification

Revision 01 02 03 04 05 06 07 08 09 10

Date 12/04/2024 24/04/2024 10/05/2024 23/05/2024 03/06/2024 11/06/2024
20/06/2024, 03/07/2024 17/07/2024 06/08/2024 22/08/2024

History

Author L O'Donnell N Padoin N Padoin N Padoin, L O'Donnell N Padoin N
Padoin N Padoin, L O'Donnell L O'Donnell N Padoin L O'Donnell N Padoin

Comment Initial issue Add Sections 3.2.5, 3.3, Update sections 3.2.3,
3.2.4 Add sections 3.4, 3.5, update counter and description in section 2
Add security levels. Merge in Wand v2 Serial Comms API Rev 19. Add
sections 3.6, 3.7, 3.8, 3.9. Replace calib value with delay value in
3.3.1, 3.3.2, 3.3.3. Update 3.2.5 Update 3.7.1, 3.7.2, 3.3.14. Add
CRC-16 Info in section 2 Update notes in 3.3, Update 3.3.1-3.3.6. Add
authentication response to 2.1 Add 3.3.4, 3.3.5, 3.3.6, 3.3.11, 3.3.12,
3.3.17, 3.3.18, 3.3.23, 3.3.24, 3.3.29, 3.3.30, 3.8.5. Add units of
delay values and some chirp parameters. Update all of 3.7 as well as
3.3.13, 3.3.14, 3.3.15, 3.3.17. Add active subscription notes to 3.2,
3.3, 3.6.2, 3.6.3, 3.8, 3.9. Add 3.1.6, 3.1.7, 3.1.8, 3.1.9, 3.1.10,
3.2.6, 3.2.7, 3.2.8, 3.2.9, 3.2.10, 3.2.11, 3.2.12, 3.2.13. Update Notes
to 3.1 and 3.1.1 Update to 3.1.4, 3.1.9 and 3.2.13 to include missing
percentage complete option Add command 3.5.3, section 3.10. Add note to
3.9.6.3. Add note about missing responses in section 2. Added clarity to
3.9.6.3 to indicate when the resulting reading is saved. Updated 3.2.6,
3.2.7, 3.2.8, 3.2.9 to note that a scan with RFID enabled must be
performed before valid data is returned. Misc formatting changes
existing wand v2 commands. Added 3.9.7

Update 3.4 to note on ordering, 3.7.1, 3.7.4, 3.7.5, 3.7.6 with new
subscription structure and notes on new asynchronous functionality,
3.9.2.3 with new asynchronous functionality, 3.10.1 to reflect multiple
keys. Add 3.2.14, 3.4.5, 3.4.6, 3.4.7, 3.5.4, 3.7.7, 3.7.8, 3.7.9,
3.9.2.6, 3.10.3

Form: BF010 Version: 3 Date: 12/12/2019

Page 6 of 109

Serial API Specification

11

29/08/2024 N Padoin

L O'Donnell

Update wording in 3.4.5 for clarity. Update 3.7.2 to note on NACK and
setting to current value. Update 3.7.6 to note on replacing a sub that
does not currently exist.

Corrected typo on 3.9.7.1 security level

12

10/09/2024 N Padoin

Add 3.5.5, 3.5.6, 3.6.5

L O'Donnell

Updated 3.1.2, 3.1.3, 3.1.7, 3.1.8, 3.2.11

and 3.2.13. Add note that high-temperature

mode for sensor types is SDL mode.

13

23/10/2024 N Padoin

Corrected security levels in messages 3.1.6,

3.4.1, 3.4.2, 3.4.3, 3.4.4, 3.5.1, 3.5.2,

3.9.7.1.

Updated 3.3.1-3.3.5 for multi-element

sensors

Added 3.2.15-3.2.18, updated 3.2.1, 3.2.2

for RFID ramp power.

Add 3.5.7, 3.11

14

30/10/2024 N Padoin

Add 3.4.8.

Update 3.4 with info referring to 3.4.8.

Update 3.4.7 to allow for lowering security

level to a specific level rather than just to

level 0.

Update 3.11.2 With specific Production Test

failure conditions

15

05/12/2024 N Padoin

Add 3.5.8, 3.11.3.

Add more error conditions to 3.7.1.

Add recommendations to 3.7.4, 3.7.6 and

3.7.8 to not delete the active subscription

without switching away from it first

16

14/03/2025 N Padoin

Update names of 3.2.1-3.2.2, 3.2.15-3.2.18

for accuracy.

Update chirp parameters in 3.3.7, 3.3.9,

3.3.12.

Remove removal note from 3.5.6.

Update 3.7.1, 3.7.6 for "no subscription"

subscriptions.

Update 3.9.6.4, 3.9.6.5 for schema 7.

Update obsolescence note in 3.10.3.

Update 3.11.3 to standardise terminology

and support ID-only EEPROM writes.

17

15/04/2025 M Thompson

3.2.1-2 & 3.2.15-18 Give example RFID

power settings (for 0.18 and 18.0 dBm)

3.5.5 Clarify Reset Fuel Gauge comment

Form: BF010 Version: 3 Date: 12/12/2019

Page 7 of 109

Reference REF 1. REF 2. REF 3. REF 4. REF 5. SHA XOR CRC AES CTR

Serial API Specification

Document

References

Wand 2.0 Serial Comms API rev19.pdf

Technical Requirements for HDC v3 1.10.pdf Software Technical
Requirements for HDC v3 1.56 INDU0238 Technical Specification Rev 3.pdf
FW_SW Command Interface Communication.docx

Author, link, attachment Spark Product Innovation Limited Inductosense
Inductosense ByteSnap Inductosense / ByteSnap

Glossary of terms Secure Hash Algorithm Exclusive-Or Cyclic Redundancy
Check Advanced Encryption Standard Counter Mode of Operation

Form: BF010 Version: 3 Date: 12/12/2019

Page 8 of 109

Serial API Specification 1 Introduction A serial communications API is
required to interface the WAND v3 to Inductosense's iDART software,
which allows the WAND v3 device to be configured and controlled. As WAND
v3 firmware is an evolution of the existing WAND v2 firmware to meet the
changes requested by Inductosense. As a significant portion of the WAND
v2 functionality shall remain in WAND v3, the existing Serial API
described by REF 1 shall be retained with the exception of the Sub-Sea
commands which were deprecated and Scan Step Commands. The
implementation of the Sub-Sea commands and Scan Step commands were not
provided to ByteSnap. The purpose of this document is to highlight any
changes to the Serial API specification such that Inductosense are able
to update their iDART software to be compatible with the WAND v3
firmware. This document is to be considered a live document and subject
to change. 2 Proposed Framing Changes The existing Serial API protocol
is a command-response architecture with no built-in message framing.
This is a risky approach, as without appropriate message framing,
firmware is unable to validate that the data it has received is what was
originally sent by the sender. Any interference on the transmission
would likely have unintended consequences. To help alleviate this, as
well as allow for a retry mechanism, ByteSnap propose that a wrapper is
added around the existing serial protocol which allows for a message
counter, length and checksum be added. This is further referenced in REF
5. If a command is not responded to, and the suspected cause is that the
message is not fully received, the best remedy short of disconnecting
and reconnecting (Bluetooth) or unplugging/re-plugging the USB (USB) is
to send KeepAlive (See 3.5.1) until it responds, or until it is
satisfactorily determined the device is otherwise not responding.

Form: BF010 Version: 3 Date: 12/12/2019

Page 9 of 109

Serial API Specification

2.1 Frame Frame (subject to change) Description Example

magic (byte), counter(byte), payloadlen(short) payload(variable)

crc(short)

magic

0x49 'I'

counter

Message counter ­ each new command increments

this counter, messages \<0x40 greater than previous

successful message are assumed to not be repeats.

This value is internally updated with each

successful message.

It is expected that a retried message is sent with

the same counter value and new messages with an

incremented counter value.

Response uses this as its own counter

payloadlen

Length of the payload only. Can be 0. (Full message

length is this + 6)

payload

The message payload, as per the existing

commands.

crc

CRC-16/CCITT-FALSE of all bytes except the CRC.

(MSB transmitted first, Polynomial 0x1021, no

refin, no refout, Initial value 0xFFFF, No final XOR)

Each message (command or response) is wrapped in this frame to allow for
much simpler message validity and repeat checking

Values: A message that is received while the processor is busy
processing a message will return a response code of 0x15 in the payload
and is not treated as a successfully processed message for the counter.
A message that is not allowed by the current authentication level will
return a response code of 0x3D, and is a successfully processed message
for the counter. A repeated message returns the same response code as
was returned then, with the top bit (0x80) set. It is also not a
successful message for the counter.

Byte Stream: For example, Do Scan (0xAA03) 0x49, 0x08, 0x00, 0x02, 0xAA,
0x03, 0x82, 0x79 Response (ack) 0x49, 0x08, 0x00, 0x01, 0x06, 0x7E, 0x2C

Form: BF010 Version: 3 Date: 12/12/2019

Page 10 of 109

Serial API Specification 3 New Commands The new commands detailed below
do not contain the proposed message frame referenced in section 2, and
as such should be considered as the payload of the proposed message
frame. 3.1 Processor Firmware Upgrade Processor Firmware Upgrade expects
a custom file format that contains a header (manifest) and one or more
files appended as binary data to the file. It currently supports, SHARC
Loader Streams (Processor / DSP Firmware), Display Assets and RFID
Firmware Upgrade. The process involves sending the Upload Start command
which instructs the device how to expect the received data. The
aforementioned file is then uploaded in blocks in a size as described by
the Upload Start command. The device expects to receive the blocks in
sequential order. It is recommended to allow a gap of 50mS between block
transmission to allow the device to handle the blocks in memory and
check that the block has been received successfully. If an upload needs
to be interrupted for any reason, simply stop sending blocks. Using the
Upload Start command will reinitialise internal state. The Start
Firmware Upgrade command starts the process of parsing the uploaded file
and upgrading the files, an upgrade may not be aborted at this step. The
Query Firmware Upgrade Status command should be used to query the state
of the upgrade.

Form: BF010 Version: 3 Date: 12/12/2019

Page 11 of 109

Serial API Specification

Firmware Upload Start

Serial Command

cmd (uint16), totalBytes (uint32), blockSize (uint16), totalBlocks

(uint16), crc32 (uint32)

cmd

0xB001

totalBytes

Total number of bytes of the entire file

blockSize

Size of each individual block. Variable to allow for

tuning of the transmission, maximum of 2048 bytes.

Last block size may be less than this.

totalBlocks Total number of expected blocks

Crc32

Crc32 of the entire firmware file, See 3.1.5 for crc32

polynomial, seed and xor details.

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description Security Level Example

This command is used to initialise a firmware upload. Level 2 Byte
Stream: cmd(MSB), cmd(LSB), totalBytes(MSB), totalBytes(LSB+2),
totalBytes(LSB+1), totalBytes(LSB), blockSize(MSB), blockSize(LSB),
totalBlocks(MSB), totalBlocks(LSB), crc32(MSB), crc32(LSB+2),
crc32(LSB+1), crc32(LSB)

0xB0, 0x01, 0x00, 0x0F, 0x0F, 0xE0, 0x04, 0x00, 0x03, 0xC3, 0x9D, 0xF5,
0xDF, 0x10

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 12 of 109

Serial API Specification

Firmware Block Upload

Serial Command

cmd (uint16), blockIdx (uint16), blockSize (uint16), crc32 (uint32),

blockData

cmd

0xB002

blockIdx

Index of the current block to transmit

blockSize

Size of this block this should match the blockSize sent

in in the Firmware Upload Start command, unless it is

the final block.

Crc32

Crc32 of this block, See 3.1.5 for crc32 polynomial, seed

and xor details.

blockData

Binary data of the firmware file

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

No-acknowledge (uint8)

No-

0x21: NACK byte if unsuccessful

acknowledge

Description

This command is used to transmit a block of firmware data. Blocks should
be sent sequentially.

A NACK shall be returned if the block size does not match the initially
set block size. A NACK shall be returned in the Crc32 fails due to
transmission errors. A NACK shall be returned if the block id does not
match the expected block id. Retries are allowed and will not NACK.

Security Level Example

A delay of 35ms or greater should be used between block transmissions to
allow for file writing time. Level 2 Byte Stream: cmd(MSB), cmd(LSB),
blockIdx(MSB), blockIdx(LSB), blockSize(MSB), blockSize(LSB),
crc32(MSB), crc32(LSB+2), crc32(LSB+1), crc32(LSB), blockData...

0xB0, 0x02, 0x00, 0x01, 0x04, 0x00, 0x21, 0x00, 0x78, 0x6A, ...

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 13 of 109

Serial API Specification

Start Firmware Upgrade

Serial Command cmd (uint16)

cmd

0xB003

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

No-acknowledge (uint8)

No-

0x21: NACK byte if unsuccessful

acknowledge

Description

This command is used to start the firmware upgrade process.

A NACK shall be returned if the previously uploaded file was invalid. A
NACK shall be returned if no firmware upgrade file was uploaded.

Security Level Example

Note that 15 seconds after a successful firmware upgrade, the device
will automatically reboot. Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xB0, 0x03

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 14 of 109

Serial API Specification

Query Firmware Upgrade Status

Serial Command cmd (uint16)

cmd

0xB004

Response

acknowledge (uint8), status (uint8), percentage (uint8)

acknowledge 0x06: ACK byte if successful

status

0 = Inactive

1 = Loading Manifest

2 = Processing Files

3 = Validating Files

4 = Complete

5 = CRC Failure

6 = Invalid Format

7 = Validation Failure

8 = Internal Failure

percentage Indicates progress completion, valid values from 0-100

Description

This command is used to query the current state of a firmware upgrade

Security Level Example

Inactive ­ A firmware upgrade is not in progress Loading Manifest ­
Extracts and processes the manifest embedded within the uploaded file
Processing Files ­ Extracts individual firmware / display asset elements
from the manifest and writes them to their location. Validating Files ­
Checks that the files were written correctly. Complete ­ All files were
written successfully. CRC Failure ­ CRC check of the overall file failed
Invalid Format ­ The contents of the file are malformed. Validation
Failure ­ Validation File check failed Internal Failure ­ A system error
occurred Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xB0, 0x04

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 15 of 109

Serial API Specification

CRC32

The Crc32 algorithm uses the following settings:

Initial Seed Final XOR Polynomial

0xFFFFFFFF 0xFFFFFFFF 0xEDB88320

This is functionally equivalent to using the polynomial 0x04C11DB7 but
reversing data bytes.

Form: BF010 Version: 3 Date: 12/12/2019

Page 16 of 109

Serial API Specification

Get Bootloader Info Serial Command cmd (short) cmd

0xFFF1

Response

acknowledge (byte) , bootloaderVersion (byte), bootRegistryVersion

(byte)

acknowledge

0x06: ACK byte if successful

bootloaderVersion Version number of the bootloader

bootRegistryVersion Version number of the data structure used to

manage the memory partition table

Description Security Level Example

The Get Bootloader Info command returns the version number for Level 1
Byte Stream: cmd(MSB), cmd(LSB) 0xFF, 0xF1

Form: BF010 Version: 3 Date: 12/12/2019

Page 17 of 109

Serial API Specification

Boot Logo Upload Start

Serial Command

cmd (uint16), totalBytes (uint32), blockSize (uint16), totalBlocks

(uint16), sectorOffset (uint32) ,crc32 (uint32)

cmd

0xB201

totalBytes

Total number of bytes of the entire file

blockSize

Size of each individual block. Variable to allow for

tuning of the transmission, maximum of 2048 bytes.

Last block size may be less than this.

totalBlocks Total number of expected blocks

sectorOffset The offset of the display SD card to write the boot logo

video to

Crc32

Crc32 of the entire boot logo video file, See 3.1.5 for

crc32 polynomial, seed and xor details.

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description

This command is used to initialise a boot logo video upload.

Security Level Example

Sector offsets that are currently used are as follows: Standard ­
0x005F2666 TRND ­ 0x005F6F0F China Shipbuilding ­ 0x005FBC68 Level 2 Byte
Stream: cmd(MSB), cmd(LSB), totalBytes(MSB), totalBytes(LSB+2),
totalBytes(LSB+1), totalBytes(LSB), blockSize(MSB), blockSize(LSB),
totalBlocks(MSB), totalBlocks(LSB), crc32(MSB), crc32(LSB+2),
crc32(LSB+1), crc32(LSB)

0xB2, 0x01, 0x00, 0x0F, 0x0F, 0xE0, 0x04, 0x00, 0x03, 0xC3, 0x00, 0x5F,
0x6F, 0x0F 0x9D, 0xF5, 0xDF, 0x10

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 18 of 109

Serial API Specification

Boot Logo Block Upload

Serial Command

cmd (uint16), blockIdx (uint16), blockSize (uint16), crc32 (uint32),

blockData

cmd

0xB202

blockIdx

Index of the current block to transmit

blockSize

Size of this block this should match the blockSize sent

in in the Boot Logo Upload Start command, unless it is

the final block.

Crc32

Crc32 of this block, See 3.1.5 for crc32 polynomial, seed

and xor details.

blockData

Binary data of the boot logo video file

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

No-acknowledge (uint8)

No-

0x21: NACK byte if unsuccessful

acknowledge

Description

This command is used to transmit a block of boot logo video data. Blocks
should be sent sequentially.

A NACK shall be returned if the block size does not match the initially
set block size. A NACK shall be returned in the Crc32 fails due to
transmission errors. A NACK shall be returned if the block id does not
match the expected block id. Retries are allowed and will not NACK.

Security Level Example

A delay of 35ms or greater should be used between block transmissions to
allow for file writing time. Level 2 Byte Stream: cmd(MSB), cmd(LSB),
blockIdx(MSB), blockIdx(LSB), blockSize(MSB), blockSize(LSB),
crc32(MSB), crc32(LSB+2), crc32(LSB+1), crc32(LSB), blockData...

0xB2, 0x02, 0x00, 0x01, 0x04, 0x00, 0x21, 0x00, 0x78, 0x6A, ...

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 19 of 109

Serial API Specification

Start Boot Logo Update

Serial Command cmd (uint16)

cmd

0xB203

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

No-acknowledge (uint8)

No-

0x21: NACK byte if unsuccessful

acknowledge

Description

This command is used to start the boot logo flash process.

Security Level Example

A NACK shall be returned if the previously uploaded file was invalid. A
NACK shall be returned if no boot logo video file was uploaded. Level 2
Byte Stream: cmd(MSB), cmd(LSB)

0xB2, 0x03

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 20 of 109

Serial API Specification

Query Boot Logo Update Status

Serial Command cmd (uint16)

cmd

0xB004

Response

acknowledge (uint8), status (uint8), percentage (uint8)

acknowledge 0x06: ACK byte if successful

status

0 = Inactive

1 = Setting Sector

2 = Writing Logo

3 = Reserved

4 = Complete

5 = CRC Failure

6 = Set Sector Failed

7 = Read File Failed

8 = Write Sector Failed

9 = Failed

percentage Indicates progress completion, valid values from 0-100

Description

This command is used to query the current state of a firmware upgrade

Security Level Example

Inactive ­ A boot logo upgrade is not in progress Setting Sector ­
Instructs the display module to set the sector that we want to write to
Writing Logo ­ Writes the boot logo file to the Display SD card Reserved ­
Reserved. Complete ­ Boot logo was written successfully. CRC Failure ­ CRC
check of the overall file failed Set Sector Failed ­ Display reported
that it was unable to set the sector to write to. Read File Failed ­
Failed to read the file from internal memory Write Sector Failure ­
Display reported an error when writing to the sector Failed ­ A write
failure occurred when a chunk was uploaded Level 2 Byte Stream:
cmd(MSB), cmd(LSB)

0xB2, 0x04

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 21 of 109

Serial API Specification

3.2 RFID All RFID commands are relative to the active subscription Get
RFID Read Tx Power

Serial Command cmd (uint16)

cmd

0xAA06

Response

acknowledge (uint8), power (uint16)

acknowledge 0x06: ACK byte if successful

power

Power level in increments of 0.01 dBm

Description

This command is used to get the current maximum power level set on the
RFID module. Ramping power will ramp no higher than this.

Security Level Example

It returns the power level set on the RFID module in increments of 0.01
dBm (e.g. 18 indicates 0.18 dBm and 1800 indicates 18.00 dBm) Level 2
Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x06

The response would be 0x06

Set RFID Read Tx Power

Serial Command

cmd (uint16), power(uint16)

cmd

0xAA07

power

Power level in 0.01 dBm increments

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description

This command is used to set the current maximum power level on the RFID
module. Ramping power will ramp no higher than this.

Security Level Example

The power level should be set to increments of 0.01dBm (e.g. 18 sets
0.18 dBm and 1800 sets 18.00 dBm). Level 2 Byte Stream: cmd(MSB),
cmd(LSB)

0xAA, 0x07

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 22 of 109

Serial API Specification

Get RFID Region Serial Command cmd (uint16) cmd

0xAA08

Response

acknowledge (uint8), region (uint8)

acknowledge 0x06: ACK byte if successful

region

North America: 1

ETSI Lower (EU 868): 8

India: 4

Korea: 9

PRC: 6

Australia: 11

New Zealand: 12

Japan: 5

Custom: 255

Description

This command is used to get the current region that the RFID module is
set to.

Security Level Example

See Set RFID Region Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x08

An example response would be 0x06 0x08

Form: BF010 Version: 3 Date: 12/12/2019

Page 23 of 109

Serial API Specification

Set RFID Region

Serial Command

cmd (uint16), region(uint8)

cmd

0xAA09

region

North America: 1

ETSI Lower (EU 868): 8

India: 4

Korea: 9

PRC: 6

Australia: 11

New Zealand: 12

Japan: 5

Custom: 255

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description

This command is used to set the current region of the RFID module.

The region value should be set to one of the values in the above table,
with Custom implying that the hop table has been properly set (will
default to ETSI Lower (8) if the hop table is invalid)

Security Level Example

Any value other than the above shall be considered as ETSI Lower (8)
Level 2 Byte Stream: cmd(MSB), cmd(LSB) region

0xAA, 0x09, 0x08

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 24 of 109

Serial API Specification

Set RFID Hop Table

Serial Command

cmd (uint16), hop count(uint8), frequency quantisation (uint8), hops

(uint32)(up to 16)

cmd

0xAA10

hop count

Minimum 2, Maximum 16, is expected to match the

count in the rest of the message

frequency quantisation hops

in kHz, 50, 100, 125 and 250 are valid values Each frequency hop in kHz.
Maximum of 16. 860000930000 are valid values. Values are rounded to the
nearest frequency quantisation

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description Security Level Example

This command is used to set the hop table for the custom RFID region. It
is expected that this meets regulatory conditions in the target region.
Level 2 Byte Stream: cmd(MSB), cmd(LSB), (hop count), (quantisation)
hop[0](byte0) hop\[0\] (byte1) hop[0](byte2) hop\[0\] (byte3) hop\[1\]
(byte0) (...)

0xAA, 0x10, 0x02, 0x32, 0x3C, 0x3E, 0x0D, 0x00, 0x04, 0x3F, 0x0D, 0x00
The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 25 of 109

Serial API Specification

Get RFID Software Version

Serial Command cmd (uint16)

cmd

0xAA11

Response

acknowledge (uint8), versionInfo (string) acknowledge 0x06: ACK byte if
successful versionInfo 3 sets of 11 characters indicating the firmware
version, firmware date and bootloader version.

Description

This command is used to get the software version information reported by
the RFID module. The first 11 characters indicate the firmware version,
in the example below this is: 02.01.01.1A The next 11 characters
indicate the firmware date, in the example below this is 20.23.05.03 The
next 11 characters indicate the bootloader version, in the example below
this is 23.01.06.00

Security Level Example

A measurement scan with RFID enabled should be performed before
attempting to read these values back, otherwise the data shall return 0.
Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x11

An example response would be 0x06 0x30 0x32 0x2E 0x30 0x31 0x2E 0x30
0x31 0x2E 0x31 0x41 0x32 0x30 0x2E 0x32 0x33 0x2E 0x30 0x35 0x2E 0x30
0x33 0x32 0x33 0x2E 0x30 0x31 0x2E 0x30 0x36 0x2E 0x30 0x30

Form: BF010 Version: 3 Date: 12/12/2019

Page 26 of 109

Serial API Specification

Get RFID Hardware Version

Serial Command cmd (uint16)

cmd

0xAA12

Response

acknowledge (uint8), versionInfo (string) acknowledge 0x06: ACK byte if
successful versionInfo 11 characters indicating the hardware version

Description

This command is used to get the hardware version information reported by
the RFID module.

In the example below this is: 38.00.00.01

Security Level Example

A measurement scan with RFID enabled should be performed before
attempting to read these values back, otherwise the data shall return 0.
Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x12

An example response would be 0x06 0x33 0x38 0x2E 0x30 0x30 0x2E 0x30
0x30 0x2E 0x30 0x31

Form: BF010 Version: 3 Date: 12/12/2019

Page 27 of 109

Serial API Specification

Get RFID Serial Number

Serial Command cmd (uint16)

cmd

0xAA13

Response

acknowledge (uint8), serial (string)

acknowledge 0x06: ACK byte if successful

serial

32 characters indicating the serial number of the device

Description

This command is used to get the serial number reported by the RFID
module.

In the example below this is 382310045902275075+

Security Level Example

A measurement scan with RFID enabled should be performed before
attempting to read these values back, otherwise the data shall return 0.
Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x13

An example response would be 0x06 0x33 0x38 0x32 0x33 0x31 0x30 0x30
0x34 0x35 0x39 0x30 0x32 0x32 0x37 0x35 0x30 0x37 0x35 0x2B 0x00 0x00
0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00

Form: BF010 Version: 3 Date: 12/12/2019

Page 28 of 109

Serial API Specification

Get RFID Model

Serial Command cmd (uint16)

cmd

0xAA14

Response

acknowledge (uint8), model (string) acknowledge 0x06: ACK byte if
successful versionInfo 32 characters indicating the model of the RFID
module

Description

This command is used to get the module information reported by the RFID
module.

In the example below this is M7e Pico

Security Level Example

A measurement scan with RFID enabled should be performed before
attempting to read these values back, otherwise the data shall return 0.
Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x14

An example response would be 0x06 0x4D 0x37 0x65 0x20 0x50 0x69 0x63
0x6F 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00
0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00

Form: BF010 Version: 3 Date: 12/12/2019

Page 29 of 109

Serial API Specification

RFID Firmware Upload Start

This is provided as an alternative mechanism to upload the RFID firmware
file directly. It is not necessary to use this mechanism.

Serial Command

cmd (uint16), totalBytes (uint32), blockSize (uint16), totalBlocks

(uint16), crc32 (uint32)

cmd

0xB301

totalBytes

Total number of bytes of the entire file

blockSize

Size of each individual block. Variable to allow for

tuning of the transmission, maximum of 2048 bytes.

Last block size may be less than this.

totalBlocks Total number of expected blocks

Crc32

Crc32 of the entire RFID firmware file, See 3.1.5 for

crc32 polynomial, seed and xor details.

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description Security Level Example

This command is used to initialise a firmware upload. Level 2 Byte
Stream: cmd(MSB), cmd(LSB), totalBytes(MSB), totalBytes(LSB+2),
totalBytes(LSB+1), totalBytes(LSB), blockSize(MSB), blockSize(LSB),
totalBlocks(MSB), totalBlocks(LSB), crc32(MSB), crc32(LSB+2),
crc32(LSB+1), crc32(LSB)

0xB3, 0x01, 0x00, 0x0F, 0x0F, 0xE0, 0x04, 0x00, 0x03, 0xC3, 0x9D, 0xF5,
0xDF, 0x10

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 30 of 109

Serial API Specification

RFID Firmware Upload Block

This is provided as an alternative mechanism to upload the RFID firmware
file directly. It is not necessary to use this mechanism.

Serial Command

cmd (uint16), blockIdx (uint16), blockSize (uint16), crc32 (uint32),

blockData

cmd

0xB302

blockIdx

Index of the current block to transmit

blockSize

Size of this block this should match the blockSize sent

in in the RFID Firmware Upload Start command, unless

it is the final block.

Crc32

Crc32 of this block, See 3.1.5 for crc32 polynomial, seed

and xor details.

blockData

Binary data of the firmware file

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

No-acknowledge (uint8)

No-

0x21: NACK byte if unsuccessful

acknowledge

Description

This command is used to transmit a block of firmware data. Blocks should
be sent sequentially.

A NACK shall be returned if the block size does not match the initially
set block size. A NACK shall be returned in the Crc32 fails due to
transmission errors. A NACK shall be returned if the block id does not
match the expected block id. Retries are allowed and will not NACK.

Security Level Example

A delay of 35ms or greater should be used between block transmissions to
allow for file writing time. Level 2 Byte Stream: cmd(MSB), cmd(LSB),
blockIdx(MSB), blockIdx(LSB), blockSize(MSB), blockSize(LSB),
crc32(MSB), crc32(LSB+2), crc32(LSB+1), crc32(LSB), blockData...

0xB3, 0x02, 0x00, 0x01, 0x04, 0x00, 0x21, 0x00, 0x78, 0x6A, ...

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 31 of 109

Serial API Specification

Start RFID Firmware Upgrade

This is provided as an alternative mechanism to upload the RFID firmware
file directly. It is not necessary to use this mechanism.

Serial Command cmd (uint16)

cmd

0xB303

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

No-acknowledge (uint8)

No-

0x21: NACK byte if unsuccessful

acknowledge

Description

This command is used to start the RFID firmware upgrade process.

Security Level Example

A NACK shall be returned if the previously uploaded file was invalid. A
NACK shall be returned if no firmware upgrade file was uploaded. Level 2
Byte Stream: cmd(MSB), cmd(LSB)

0xB3, 0x03

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 32 of 109

Serial API Specification

Query RFID Firmware Upgrade Status

This is provided as an alternative mechanism to upload the RFID firmware
file directly. This can be used alongside the Query Firmware Upgrade
Status message if a RFID file is part of the custom file used for the
upgrade process.

Serial Command cmd (uint16)

cmd

0xB304

Response

acknowledge (uint8), status (uint8), percentage (uint8)

acknowledge 0x06: ACK byte if successful

status

0 = Inactive

1 = In Progress

2 = Reserved

3 = Reserved

4 = Complete

5 = CRC Failure

6 = RFID Upload Initialisation Failed

7 = Read File Failed

8 = Upload Failed

9 = Invalid File

10 ­ Missing Cartridge

percentage Indicates progress completion, valid values from 0-100

Description

This command is used to query the current state of a firmware upgrade

Security Level Example

Inactive ­ A RFID firmware upgrade is not in progress In Progress ­ RFID
Moduel is being flashed Reserved ­ Reserved Complete ­ The RFID module was
flashed CRC Failure ­ CRC check of the overall file failed RFID Upload
Initialisation Failed ­ Failed to initialise the Firmware Update process
on the RFID module Read File Failure ­ Failed to read the RFID firmware
file on internal memory Upload Failed ­ A failure occurred while writing
to RFID flash Invalid File ­ An invalid file was supplied to the RFID
firmware parser Missing Cartridge ­ A communications issue with detected
when talking to the RFID module Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xB3, 0x04

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 33 of 109

Serial API Specification

Get RFID Hop Table Serial Command cmd (uint16) cmd

0xAA16

Response

acknowledge (uint8), frequency quantisation (uint32), hops
(uint32\[16\])

acknowledge 0x06: ACK byte if successful

frequency

uint32: in kHz, 50, 100, 125 and 250 are valid values

quantisation

hops

Each uint32 frequency hop in kHz. 860000-930000 are

valid values. Values are rounded to the nearest

frequency quantisation. There are 16 of these, with the

valid values being those before the first 0

Description Security Level Example

This command is used to get the hop table for the custom RFID region.
The used values in this table are those before the first uint32 value
that is numerically 0. All values after this are not used by the wand.
Level 2 Byte Stream: cmd(MSB), cmd(LSB) 0xAA 0x16 The response would be
0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 34 of 109

Serial API Specification

Get RFID Ramp Start Read Tx Power

Serial Command cmd (uint16)

cmd

0xAA17

Response

acknowledge (uint8), power (uint16)

acknowledge 0x06: ACK byte if successful

power

Power level in increments of 0.01 dBm

Description

This command is used to get the ramp start power level set on the RFID
module. This is the starting power of the RFID read. The system performs
the first read at this power level, then increments the power by
Per-Ramp Power each read until it reaches the max power set in 3.2.2

Security Level Example

It returns the power level set on the RFID module in increments of 0.01
dBm (e.g. 18 indicates 0.18 dBm and 1800 indicates 18.00 dBm). Level 2
Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x17

The response would be 0x06

Set RFID Ramp Start Read Tx Power

Serial Command cmd (uint16), power(uint16)

cmd

0xAA18

power

Power level in 0.01 dBm increments

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description

This command is used to set the ramp start power level on the RFID
module. This is the starting power of the RFID read. The system performs
the first read at this power level, then increments the power by
Per-Ramp Power each read until it reaches the max power set in 3.2.2
This value cannot be set higher than Rx Power.

Security Level Example

The power level should be set to increments of 0.01 dBm (e.g. 18 sets
0.18 dBm and 1800 sets 18.00 dBm). Level 2 Byte Stream: cmd(MSB),
cmd(LSB)

0xAA, 0x18

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 35 of 109

Serial API Specification

Get RFID Per-Ramp Read Tx Power

Serial Command cmd (uint16)

cmd

0xAA19

Response

acknowledge (uint8), power (uint16)

acknowledge 0x06: ACK byte if successful

power

Power level in increments of 0.01 dBm

Description

This command is used to get the current per-ramp power level set on the
RFID module. This is the starting power of the RFID read. The system
increments the RFID Rx power by this value each read until it reaches
the max power set in 3.2.2 or a tag is found.

Security Level Example

It returns the power level set on the RFID module in increments of 0.01
dBm (e.g. 18 indicates 0.18 dBm and 1800 indicates 18.00 dBm). Level 2
Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x19

The response would be 0x06

Set RFID Per-Ramp Read Tx Power

Serial Command cmd (uint16), power(uint16)

cmd

0xAA1A

power

Power level in 0.01 dBm increments

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description

This command is used to set the current per-ramp power level set on the
RFID module. This is the starting power of the RFID read. The system
increments the RFID Rx power by this value each read until it reaches
the max power set in 3.2.2 or a tag is found.

Security Level Example

The power level should be set to increments of 0.01 dBm. Set this to the
same value as Rx Power (3.2.2) to disable ramping (e.g. 18 sets 0.18 dBm
and 1800 sets 18.00 dBm) Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x1A

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 36 of 109

Serial API Specification

3.3 Dynamic Tables When setting up the database, the order must be:
cartridges, (wand v3) chirps, sensor types, materials and finally
locations. When RFID is disabled, cartridges use the first chirp in the
table which match the cartridge's frequency. All Dynamic Table commands
are relative to the current active subscription

Get First Sensor Location

This command has changed in Wand v3 (From GetFirstSensor)

Serial Command cmd (short)

cmd

0xF101

Response

acknowledge (byte), rfid (string), material index (short),

multiSensorIndex (short), location (string)

acknowledge

0x06: ACK byte if successful

rfid

A 12-byte sequence.

material index An index indicating the material where sensor

installed.

It is expected this value already exists in the Wand

v3

multiSensorIndex Which sensor this is in a multi-sensor array (0 for

non-multisensor, 1 for first sensor up to 4 for fourth

sensor)

location

A 32-character string, pad unused characters with

zeros.

Description Security Level Example

The GetFirstSensor command returns the first sensor location entry in
the WAND 3.0 sensor database. Use this command to start getting a list
of sensors that are installed in the database. See also command
GetNextSensor. Note, if there are no sensors installed, the command will
return NAK (0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF1, 0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 37 of 109

Serial API Specification

Get Next Sensor Location

This command has changed in Wand v3 (From GetNextSensor)

Serial Command cmd (short)

cmd

0xF102

Response

acknowledge (byte), rfid (string), material index (short),

multiSensorIndex (short), location (string)

acknowledge

0x06: ACK byte if successful

rfid

A 12-byte sequence.

material index An index indicating the material where sensor

installed.

It is expected this value already exists in the Wand

v3

multiSensorIndex Which sensor this is in a multi-sensor array (0 for

non-multisensor, 1 for first sensor up to 4 for fourth

sensor)

location

A 32-character string, pad unused characters with

zeros.

Description Security Level Example

The GetNextSensor command returns the next sensor location entry in the
WAND 3.0 user database. Use this command to get a list of sensors that
are installed in the database. See also command GetFirstSensor. Note if
you have reached the end of the list, this command returns NAK (0x21)
Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF1, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 38 of 109

Serial API Specification

Add Sensor Location

This command has changed in Wand v3 (From AddSensor)

Serial Command cmd (short), rfid (string), material index (short),
multiSensorIndex

(short), location (string)

cmd

0xF103

acknowledge

0x06: ACK byte if successful

rfid

A 12-byte sequence.

material index An index indicating the material where sensor

installed.

It is expected this value already exists in the Wand

v3

multiSensorIndex Which sensor this is in a multi-sensor array (0 for

non-multisensor, 1 for first sensor up to 4 for fourth

sensor).

It is assumed that for a given multi-element sensor,

each individual sensor has consecutive RFID

numbers and each is added to the system in order of

sensor 1 to sensor 4.

location

A 32-character string, pad unused characters with

zeros.

Response Description Security Level Example

acknowledge (byte)

acknowledge

0x06: ACK byte if successful

The AddSensor command adds a sensor location to the WAND 2.0 sensor
location database. If the operation is successful, ACK is returned, else
a NAK (0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB)

0xF1, 0x03

Form: BF010 Version: 3 Date: 12/12/2019

Page 39 of 109

Serial API Specification

Get Sensor Location At Index

Serial Command cmd (short), index (uint16)

cmd

0xF107

index

The index in the database to get

Response

acknowledge (byte), rfid (string), material index (short),

multiSensorIndex (short), location (string)

acknowledge

0x06: ACK byte if successful

rfid

A 12-byte sequence.

material index An index indicating the material where sensor

installed.

It is expected this value already exists in the Wand

v3

multiSensorIndex Which sensor this is in a multi-sensor array (0 for

non-multisensor, 1 for first sensor up to 4 for fourth

sensor)

location

A 32-character string, pad unused characters with

zeros.

Description Security Level Example

The GetSensorLocationAtIndex command returns the sensor location entry
in the WAND 3.0 user database at the given index. Use this command to
get a sensor location installed in the database. Note if you have
requested a sensor location not in the table, this command returns NAK
(0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF1, 0x07

Form: BF010 Version: 3 Date: 12/12/2019

Page 40 of 109

Serial API Specification

Replace Sensor Location

This command has changed in Wand v3 (From AddSensor)

Serial Command cmd (short), rfid (string), material index (short),
multiSensorIndex

(short), location (string), index (uint16)

cmd

0xF108

acknowledge

0x06: ACK byte if successful

rfid

A 12-byte sequence.

material index An index indicating the material where sensor

installed.

It is expected this value already exists in the Wand

v3

multiSensorIndex Which sensor this is in a multi-sensor array (0 for

non-multisensor, 1 for first sensor up to 4 for fourth

sensor).

It is assumed that for a given multi-element sensor,

each individual sensor has consecutive RFID

numbers and each is added to the system in order of

sensor 1 to sensor 4.

location

A 32-character string, pad unused characters with

zeros.

index

The index in the database to replace

Response Description Security Level Example

acknowledge (byte)

acknowledge

0x06: ACK byte if successful

The ReplaceSensorLocation command replaces a sensor location in the WAND
2.0 sensor location database. If the operation is successful, ACK is
returned, else a NAK (0x21) where the operation fails. Level 1 Byte
Stream: cmd(MSB), cmd(LSB)

0xF1, 0x08

Form: BF010 Version: 3 Date: 12/12/2019

Page 41 of 109

Serial API Specification

Delete All Sensors Serial Command cmd (short) cmd

0xF109

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The DeleteAllSensors command deletes all sensor locations from the WAND
2.0 sensor location database. If the operation is successful, ACK is
returned, else a NAK (0x21) where the operation fails. Level 1 Byte
Stream: cmd(MSB), cmd(LSB)

0xF1, 0x09

Form: BF010 Version: 3 Date: 12/12/2019

Page 42 of 109

Serial API Specification

Get First Chirp (Wand v3)

Serial Command cmd (short)

cmd

0xF303

Response

acknowledge (byte), Equivalent Cycles(float32), Stretch Factor

(float32), Amp Scalar (float32), Centre Frequency(uint32), Sample

Count(uint32), Sample Freq(uint32),

acknowledge 0x06: ACK byte if successful

Equivalent

float32:

Cycles

Stretch Factor float32:

Amp Scalar float32:

Centre

uint32: Centre frequency of the chirp in Hz

Frequency

Sample Count uint32: Number of samples in the chirp. Must be

215(32768), 216(65536), 217(131072) or 218(262144) at

this time

Sample Freq uint32: Sampling frequency of the chirp in Hz. Must be

33MHz at this time

Description Security Level Example

The GetFirstChirp command returns the first chirp (wand v3) entry in the
WAND 3.0 database. Use this command to start getting a list of chirps
(wand v3) that are installed in the database. See also command
GetNextChirp. Note, if there are no chirps present, the command will
return NAK (0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF3, 0x03

Form: BF010 Version: 3 Date: 12/12/2019

Page 43 of 109

Serial API Specification

Get Next Chirp (Wand v3)

Serial Command cmd (short)

cmd

0xF304

Response

acknowledge (byte), Equivalent Cycles(float32), Stretch Factor

(float32), Amp Scalar (float32), Centre Frequency(uint32), Sample

Count(uint32), Sample Freq(uint32),

acknowledge 0x06: ACK byte if successful

Equivalent

float32:

Cycles

Stretch Factor float32:

Amp Scalar float32:

Centre

uint32: Centre frequency of the chirp in Hz

Frequency

Sample Count uint32: Number of samples in the chirp.

Sample Freq uint32: Sampling frequency of the chirp in Hz.

Description Security Level Example

The GetNextChirp command returns the next chirp (wand v3) entry in the
WAND 3.0 database. Use this command to start getting a list of chirps
(wand v3) that are installed in the database. See also GetFirstChirp
command. Note, if you have reached the end of the list, the command will
return NAK (0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF3, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 44 of 109

Serial API Specification

Add Chirp (Wand v3)

Serial Command cmd (short), Equivalent Cycles(float32), Stretch Factor
(float32), Amp

Scalar (float32), Centre Frequency(uint32), Sample Count(uint32),

Sample Freq(uint32),

cmd

0xF305

Equivalent

float32:

Cycles

Stretch Factor float32:

Amp Scalar float32:

Centre

uint32: Centre frequency of the chirp in Hz

Frequency

Sample Count uint32: Number of samples in the chirp. Must be

215(32768), 216(65536), 217(131072) or 218(262144) at

this time

Sample Freq uint32: Sampling frequency of the chirp in Hz. Must be

33MHz at this time

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The AddChirp command adds a (wand v3) chirp to the WAND 3.0 database. If
the operation is successful, ACK is returned, else a NAK (0x21) where
the operation fails. Level 1 Byte Stream: cmd(MSB), cmd(LSB)

0xF3, 0x05

Clear Chirp Table (Wand v3)

Serial Command cmd (short)

cmd

0xF306

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The DeleteChirpTable command clears the Wand 3.0 Chirp (wand v3)
database table. If the operation is successful, ACK is returned, else a
NAK (0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xF3, 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 45 of 109

Serial API Specification

Get Chirp (Wand v3) At Index

Serial Command cmd (short) , index (uint16)

cmd

0xF307

Index

The index in the database to retrieve

Response

acknowledge (byte), Equivalent Cycles(float32), Stretch Factor

(float32), Amp Scalar (float32), Centre Frequency(uint32), Sample

Count(uint32), Sample Freq(uint32),

acknowledge 0x06: ACK byte if successful

Equivalent

float32:

Cycles

Stretch Factor float32:

Amp Scalar float32:

Centre

uint32: Centre frequency of the chirp in Hz

Frequency

Sample Count uint32: Number of samples in the chirp.

Sample Freq uint32: Sampling frequency of the chirp in Hz.

Description Security Level Example

The GetChirpAtIndex command returns the chirp (wand v3) entry in the
WAND 3.0 database for the given index. Use this command to get a chirp
(wand v3) that is installed in the database. Note if you have requested
a chirp not in the table, the command will return NAK (0x21) Level 1
Byte Stream: cmd(MSB), cmd(LSB) 0xF3, 0x07

Form: BF010 Version: 3 Date: 12/12/2019

Page 46 of 109

Serial API Specification

Replace Chirp (Wand v3) At Index

Serial Command cmd (short), Equivalent Cycles(float32), Stretch Factor
(float32), Amp

Scalar (float32), Centre Frequency(uint32), Sample Count(uint32),

Sample Freq(uint32), index (uint16)

cmd

0xF308

Equivalent

float32:

Cycles

Stretch Factor float32:

Amp Scalar float32:

Centre

uint32: Centre frequency of the chirp in Hz

Frequency

Sample Count uint32: Number of samples in the chirp. Must be

215(32768), 216(65536), 217(131072) or 218(262144) at

this time

Sample Freq uint32: Sampling frequency of the chirp in Hz. Must be

33MHz at this time

Index

The index in the database to replace

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ReplaceChirpAtIndex command replaces a (wand v3) chirp in the WAND
3.0 database. If the operation is successful, ACK is returned, else a
NAK (0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xF3, 0x08

Form: BF010 Version: 3 Date: 12/12/2019

Page 47 of 109

Serial API Specification

Get First Material Serial Command cmd (short) cmd

0xF801

Response

acknowledge (byte), Name (32-char non-null-terminated string),

Longitudinal Velocity (float32), Shear Velocity (float32), Index
(uint16)

acknowledge 0x06: ACK byte if successful

Name

32-char non-null-terminated string: The display name

of the material. Pad unused characters with zeros.

Longitudinal float32: Longitudinal velocity in m/s

Velocity

Shear Velocity float32: Shear velocity in m/s

Index

uint16: The index used to access this material (Custom

materials start at 0xFFF0)

Description Security Level Example

The GetFirstMaterial command returns the first material entry in the
WAND 3.0 database. Use this command to start getting a list of materials
that are installed in the database. See also command GetNextMaterial.
Note, if there are no materials set, the command will return NAK (0x21)
Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF8, 0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 48 of 109

Serial API Specification

Get Next Material Serial Command cmd (short) cmd

0xF802

Response

acknowledge (byte), Name (32-char non-null-terminated string),

Longitudinal Velocity (float32), Shear Velocity (float32), Index
(uint16)

acknowledge 0x06: ACK byte if successful

Name

32-char non-null-terminated string: The display name

of the material. Pad unused characters with zeros.

Longitudinal float32: Longitudinal velocity in m/s

Velocity

Shear Velocity float32: Shear velocity in m/s

Index

uint16: The index used to access this material (Custom

materials start at 0xFFF0)

Description Security Level Example

The GetNextMaterial command returns the next material entry in the WAND
3.0 database. Use this command to get the next entry in the list of
materials that are installed in the database. See also command
GetFirstMaterial. Note, if you have reached the end of the list, the
command will return NAK (0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB)
0xF8, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 49 of 109

Serial API Specification

Add Material

Serial Command cmd (short), Name (32-char non-null-terminated string),
Longitudinal

Velocity (float32), Shear Velocity (float32), Flags (uint8)

cmd

0xF803

Name

32-char non-null-terminated string: The display name

of the material. Pad unused characters with zeros.

Longitudinal float32: Longitudinal velocity in m/s

Velocity

Shear Velocity float32: Shear velocity in m/s

Flags

uint8: Special bitflags for this material:

0x01: This material is custom and will be placed in the

custom material indexes

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The AddMaterial command adds a new material to the WAND 3.0 database. If
the operation is successful, ACK is returned, else a NAK (0x21) where
the operation fails. Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF8, 0x03

Clear Material Table Serial Command cmd (short) cmd

0xF804

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ClearMaterialTable command clears the Wand 3.0 Material database
table. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xF8, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 50 of 109

Serial API Specification

Get Material At Index

Serial Command cmd (short) , index (uint16)

cmd

0xF807

index

The index in the database to retrieve

Response

acknowledge (byte), Name (32-char non-null-terminated string),

Longitudinal Velocity (float32), Shear Velocity (float32), Index
(uint16)

acknowledge 0x06: ACK byte if successful

Name

32-char non-null-terminated string: The display name

of the material. Pad unused characters with zeros.

Longitudinal float32: Longitudinal velocity in m/s

Velocity

Shear Velocity float32: Shear velocity in m/s

Index

uint16: The index used to access this material (Custom

materials start at 0xFFF0)

Description Security Level Example

The GetMaterialAtIndex command returns the requested material entry in
the WAND 3.0 database. Use this command to get an entry in the list of
materials that are installed in the database. Note, if you have
requested past the end of the list, the command will return NAK (0x21)
Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF8, 0x07

Form: BF010 Version: 3 Date: 12/12/2019

Page 51 of 109

Serial API Specification

Replace Material

Serial Command cmd (short), Name (32-char non-null-terminated string),
Longitudinal

Velocity (float32), Shear Velocity (float32), index (uint16)

cmd

0xF808

Name

32-char non-null-terminated string: The display name

of the material. Pad unused characters with zeros.

Longitudinal float32: Longitudinal velocity in m/s

Velocity

Shear Velocity float32: Shear velocity in m/s

Index

The index in the database to replace

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ReplaceMaterial command replaces a material in the WAND 3.0
database. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xF8, 0x08

Form: BF010 Version: 3 Date: 12/12/2019

Page 52 of 109

Serial API Specification

Get First Sensor Type Serial Command cmd (short) cmd

0xF901

Response

acknowledge (byte), Prefix (uint8\[3\]), Postfix(uint8\[2\]), Postfix

Operator (uint8), Name(24-char non-null-terminated string), Coil

Freq(uint16), Delay value (float32), Velocity Type (uint8), Algorithm

Type(uint8)

acknowledge 0x06: ACK byte if successful

Prefix

3 bytes that determine the RFID prefix (first 3 bytes of

RFID) of this sensor type

Postfix

2 bytes that define the postfix threshold (last two bytes

of RFID) for types with identical prefixes. (last two

bytes 48F4 = 48 F4)

Postfix

uint8: Operator to use to determine if the postfix

Operator

applies. If (\[sensor RFID\] \[operator\] \[Postfix\]) then it

applies

0 = Postfix is unused

1 = Not Equal (!=)

2 = Less Than (\<)

3 = Less Than Or Equal To (\<=)

4 = Equal To (==)

5 = Greater Than or Equal To (\>=)

6 = Greater Than (\>)

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of sensor type in seconds

Chirp Index uint16: The (wand v3) chirp index to use for this sensor

type.

Velocity Type uint8: Velocity Type from material

1 = Shear

2 = Longitudinal

Chirp

uint8: Chirp Algorithm Type

Algorithm

1 = Normal

Type

2 = High-temperature (SDL)

Description Security Level Example

The GetFirstSensorType command returns the first sensor type entry in
the WAND 3.0 database. Use this command to start getting a list of
sensor types that are installed in the database. See also command
GetNextSensorType. Note, if there are no sensor types set, the command
will return NAK (0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB) 0xF9,
0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 53 of 109

Serial API Specification

Get Next Sensor Type Serial Command cmd (short) cmd

0xF902

Response

acknowledge (byte), Prefix (uint8\[3\]), Postfix(uint8\[2\]), Postfix

Operator (uint8), Name(24-char non-null-terminated string), Coil

Freq(uint16), Delay value (float32), Velocity Type (uint8), Algorithm

Type(uint8)

acknowledge 0x06: ACK byte if successful

Prefix

3 bytes that determine the RFID prefix (first 3 bytes of

RFID) of this sensor type

Postfix

2 bytes that define the postfix threshold (last two bytes

of RFID) for types with identical prefixes. (last two

bytes 48F4 = 48 F4)

Postfix

uint8: Operator to use to determine if the postfix

Operator

applies. If (\[sensor RFID\] \[operator\] \[Postfix\]) then it

applies

0 = Postfix is unused

1 = Not Equal (!=)

2 = Less Than (\<)

3 = Less Than Or Equal To (\<=)

4 = Equal To (==)

5 = Greater Than or Equal To (\>=)

6 = Greater Than (\>)

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of sensor type in seconds

Chirp Index uint16: The (wand v3) chirp index to use for this sensor

type.

Velocity Type uint8: Velocity Type from material

1 = Shear

2 = Longitudinal

Chirp

uint8: Chirp Algorithm Type

Algorithm

1 = Normal

Type

2 = High-temperature (SDL)

Description Security Level Example

The GetNextSensorType command returns the nextt sensor type entry in the
WAND 3.0 database. Use this command to continue getting a list of sensor
types that are installed in the database. See also command
GetFirstSensorType. Note, if you have reached the end of the list, the
command will return NAK (0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB)
0xF9, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 54 of 109

Serial API Specification

Add Sensor Type

Serial Command cmd (short), Prefix (uint8\[3\]), Postfix(uint8\[2\]),
Postfix Operator (uint8),

Name(24-char non-null-terminated string), Coil Freq(uint16), Delay

value (float32), Chirp Index (uint16), Velocity Type (uint8), Algorithm

Type(uint8)

cmd

0xF903

Prefix

3 bytes that determine the RFID prefix (first 3 bytes of

RFID) of this sensor type

Postfix

2 bytes that define the postfix threshold (last two bytes

of RFID) for types with identical prefixes. (last two

bytes 48F4 = 48 F4)

Postfix

uint8: Operator to use to determine if the postfix

Operator

applies. If (\[sensor RFID\] \[operator\] \[Postfix\]) then it

applies

0 = Postfix is unused

1 = Not Equal (!=)

2 = Less Than (\<)

3 = Less Than Or Equal To (\<=)

4 = Equal To (==)

5 = Greater Than or Equal To (\>=)

6 = Greater Than (\>)

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of sensor type in seconds

Chirp Index uint16: The (wand v3) chirp index to use for this sensor

type.

Velocity Type uint8: Velocity Type from material

1 = Shear

2 = Longitudinal

Chirp

uint8: Chirp Algorithm Type

Algorithm

1 = Normal

Type

2 = High-temperature (SDL)

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The AddSensorType command adds a new sensor type to the WAND 3.0
database. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xF9, 0x03

Form: BF010 Version: 3 Date: 12/12/2019

Page 55 of 109

Serial API Specification

Clear Sensor Type Table

Serial Command cmd (short)

cmd

0xF904

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ClearSensorTypeTable command clears the Wand 3.0 Sensor Type
database table. If the operation is successful, ACK is returned, else a
NAK (0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xF9, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 56 of 109

Serial API Specification

Get Sensor Type At Index

Serial Command cmd (short), index (uint16)

cmd

0xF907

index

Index To Retrieve

Response

acknowledge (byte), Prefix (uint8\[3\]), Postfix(uint8\[2\]), Postfix

Operator (uint8), Name(24-char non-null-terminated string), Coil

Freq(uint16), Delay value (float32), Velocity Type (uint8), Algorithm

Type(uint8)

acknowledge 0x06: ACK byte if successful

Prefix

3 bytes that determine the RFID prefix (first 3 bytes of

RFID) of this sensor type

Postfix

2 bytes that define the postfix threshold (last two bytes

of RFID) for types with identical prefixes. (last two

bytes 48F4 = 48 F4)

Postfix

uint8: Operator to use to determine if the postfix

Operator

applies. If (\[sensor RFID\] \[operator\] \[Postfix\]) then it

applies

0 = Postfix is unused

1 = Not Equal (!=)

2 = Less Than (\<)

3 = Less Than Or Equal To (\<=)

4 = Equal To (==)

5 = Greater Than or Equal To (\>=)

6 = Greater Than (\>)

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of sensor type in seconds

Chirp Index uint16: The (wand v3) chirp index to use for this sensor

type.

Velocity Type uint8: Velocity Type from material

1 = Shear

2 = Longitudinal

Chirp

uint8: Chirp Algorithm Type

Algorithm

1 = Normal

Type

2 = High-temperature (SDL)

Description Security Level Example

The GetSensorTypeAtIndex command returns the requested sensor type entry
in the WAND 3.0 database. Use this command to get a sensor type that is
installed in the database. Note, if you have requested past the end of
the list, the command will return NAK (0x21) Level 1 Byte Stream:
cmd(MSB), cmd(LSB) 0xF9, 0x07

Form: BF010 Version: 3 Date: 12/12/2019

Page 57 of 109

Serial API Specification

Replace Sensor Type

Serial Command cmd (short), Prefix (uint8\[3\]), Postfix(uint8\[2\]),
Postfix Operator (uint8),

Name(24-char non-null-terminated string), Coil Freq(uint16), Delay

value (float32), Chirp Index (uint16), Velocity Type (uint8), Algorithm

Type(uint8), index (uint16)

cmd

0xF908

Prefix

3 bytes that determine the RFID prefix (first 3 bytes of

RFID) of this sensor type

Postfix

2 bytes that define the postfix threshold (last two bytes

of RFID) for types with identical prefixes. (last two

bytes 48F4 = 48 F4)

Postfix

uint8: Operator to use to determine if the postfix

Operator

applies. If (\[sensor RFID\] \[operator\] \[Postfix\]) then it

applies

0 = Postfix is unused

1 = Not Equal (!=)

2 = Less Than (\<)

3 = Less Than Or Equal To (\<=)

4 = Equal To (==)

5 = Greater Than or Equal To (\>=)

6 = Greater Than (\>)

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of sensor type in seconds

Chirp Index uint16: The (wand v3) chirp index to use for this sensor

type.

Velocity Type uint8: Velocity Type from material

1 = Shear

2 = Longitudinal

Chirp

uint8: Chirp Algorithm Type

Algorithm

1 = Normal

Type

2 = High-temperature (SDL)

Index

Index in the database to replace

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ReplaceSensorType command replaces a sensor type in the WAND 3.0
database. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xF9, 0x08

Form: BF010 Version: 3 Date: 12/12/2019

Page 58 of 109

Serial API Specification

Get First Cartridge Type

Serial Command cmd (short)

cmd

0xFA01

Response

acknowledge (byte), Name (24-char non-null-terminated string),

Cartridge ID(uint8\[2\]), Coil Freq (uint16), Delay value (float32)

acknowledge 0x06: ACK byte if successful

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Cartridge ID 2 bytes that define the cartridge ID in EEPROM. (ID

0001 = 00 01)

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of cartridge in seconds

Description Security Level Example

The GetFirstCartridgeType command returns the first cartridge type entry
in the WAND 3.0 database. Use this command to start getting a list of
cartridge types that are installed in the database. See also command
GetNextCartridgeType. Note, if there are no cartridge types set, the
command will return NAK (0x21) Level 1 Byte Stream: cmd(MSB), cmd(LSB)
0xFA, 0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 59 of 109

Serial API Specification

Get Next Cartridge Type

Serial Command cmd (short)

cmd

0xFA02

Response

acknowledge (byte), Name (24-char non-null-terminated string),

Cartridge ID(uint8\[2\]), Coil Freq (uint16), Delay value (float32)

acknowledge 0x06: ACK byte if successful

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Cartridge ID 2 bytes that define the cartridge ID in EEPROM. (ID

0001 = 00 01)

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of cartridge in seconds

Description Security Level Example

The GetNextCartridgeType command returns the next cartridge type entry
in the WAND 3.0 database. Use this command to continue getting a list of
cartridge types that are installed in the database. See also command
GetFirstCartridgeType. Note, if you have reached the end of the list,
the command will return NAK (0x21) Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xFA, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 60 of 109

Serial API Specification

Add Cartridge Type

Serial Command cmd (short), Name (24-char non-null-terminated string),
Cartridge

ID(uint8\[2\]), Coil Freq (uint16), Delay value (float32)

cmd

0xFA03

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Cartridge ID 2 bytes that define the cartridge ID in EEPROM. (ID

0001 = 00 01)

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of cartridge in seconds

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The AddCartridgeType command adds a new cartridge type to the WAND 3.0
database. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xFA, 0x03

Clear Cartridge Type Table

Serial Command cmd (short)

cmd

0xFA04

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ClearCartridgeTypeTable command clears the Wand 3.0 Cartridge Type
table. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xFA, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 61 of 109

Serial API Specification

Get Cartridge Type At Index

Serial Command cmd (short), index (uint16)

cmd

0xFA07

index

Index to retrieve

Response

acknowledge (byte), Name (24-char non-null-terminated string),

Cartridge ID(uint8\[2\]), Coil Freq (uint16), Delay value (float32)

acknowledge 0x06: ACK byte if successful

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Cartridge ID 2 bytes that define the cartridge ID in EEPROM. (ID

0001 = 00 01)

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32: delay value of cartridge in seconds

Description Security Level Example

The GetCartridgeTypeAtIndex command returns the requested cartridge type
entry in the WAND 3.0 database. Use this command to get a cartridge type
that is installed in the database. Note, if you have requested past the
end of the list, the command will return NAK (0x21) Level 1 Byte Stream:
cmd(MSB), cmd(LSB) 0xFA, 0x07

Form: BF010 Version: 3 Date: 12/12/2019

Page 62 of 109

Serial API Specification

Replace Cartridge Type

Serial Command cmd (short), Name (24-char non-null-terminated string),
Cartridge

ID(uint8\[2\]), Coil Freq (uint16), Delay value (float32) , index
(uint16)

cmd

0xFA08

Name

24-char non-null-terminated string: The display name

of the cartridge. Pad unused characters with zeros.

Cartridge ID 2 bytes that define the cartridge ID in EEPROM. (ID

0001 = 00 01)

Coil

uint16: Coil frequency in kHz

Frequency

Delay value float32:

Index

Index to replace

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ReplaceCartridgeType command replaces a cartridge type in the WAND
3.0 database. If the operation is successful, ACK is returned, else a
NAK (0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xFA, 0x08

Form: BF010 Version: 3 Date: 12/12/2019

Page 63 of 109

Serial API Specification

3.4 Authentication Note that it is assumed at this point that the key is
16 bytes and the challenge is a random 32 bytes. In order to convert the
challenge to the response we are assuming the key to be concatenated
with itself, then XOR'd with the challenge, then the resulting 32-bytes
run through SHA256

Example for a key of: 0x00112233445566778899AABBCCDDEEFF and a challenge
of : 0xFEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210
the resulting response would be:
SHA256(0x00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF
XOR 0xFEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210)

It is assumed that iDART knows the keys of the wand it is attempting to
talk to.

The following messages need to be sent in order, where NACKs will both
cause the handshake procedure to return to the beginning and the
security level to drop to level 0 immediately. Dropping the security
level to 0 will also terminate a Bluetooth connection.

See section 3.4.8 for the alternate handshake using AES and the
principles behind the level 0 -\> level 1 transition. NACKs will still
cause the security level to drop to level 0 with the same consequences
with regards to Bluetooth.

Send Challenge

Serial Command cmd (short), challenge (32-byte array)

cmd

0x7A01

challenge

The randomly generated challenge for the Wand to

respond to

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Send Challenge command is used to verify to iDART that the wand is
genuine It is expected to then call Get Challenge Response to verify the
response Level 1 Only Byte Stream: cmd(MSB), cmd(LSB) 0x7A, 0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 64 of 109

Serial API Specification

Get Challenge Response

Serial Command cmd (short)

cmd

0x7A02

Response

acknowledge (byte), response (32-byte array)

acknowledge 0x06: ACK byte if successful

Response

The 32-byte response to the previous challenge, if

present

Description Security Level Example

The Get Challenge Response command is used to retrieve the response to a
previous Get Challenge Response command. Note that if the response is a
NACK the wand does not believe it has a current challenge to respond to,
and the response will be missing from the packet Level 1 Only Byte
Stream: cmd(MSB), cmd(LSB) 0x7A, 0x02

Request Challenge Serial Command cmd (short) cmd

0x7A03

Response

acknowledge (byte),challenge (32-byte array)

acknowledge 0x06: ACK byte if successful

Challenge

The 32-byte challenge for iDART to respond to, if

present

Description Security Level Example

The Request Challenge command is used to ask the wand to challenge iDART
so the Wand can determine that iDART is genuine. It is expected for
iDART to then calculate the response and call the Send Challenge
Response command. This will be NACKed (with no other payload) if the
Wand if the wand is expecting Send Challenge Response and the previous
challenge forgotten. Level 1 Only Byte Stream: cmd(MSB), cmd(LSB) 0x7A,
0x03

Form: BF010 Version: 3 Date: 12/12/2019

Page 65 of 109

Serial API Specification

Send Challenge Response

Serial Command cmd (short), response (32-byte array)

cmd

0x7A04

response

The response to the previous challenge

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Send Challenge Response command is used to send the response to the
previously received challenge from Request Challenge. If the command is
ACKed the wand has accepted the response and will increase its security
level. Level 1 Only Byte Stream: cmd(MSB), cmd(LSB) 0x7A, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 66 of 109

Serial API Specification Level 1 Handshake With end-to-end encryption,
the level 0-\>level 1 handshake changes, to allow for session creation.
This handshake consists of 2 messages. The first message's payload
consists of 32 bytes, 16 bytes of IV and 16 bytes of AES-CBC encrypted
random data (using that IV and the device level 1 pre-shared key). This
will be decrypted by the Wand (again using the level 1 pre-shared key)
and the number used to generate a 48-byte response as follows (Plus the
usual framing + acknowledgement): 16 Bytes of IV for this AES-CBC packet
16 encrypted bytes being: The number received, incremented by one as if
it was a 128-bit Unsigned Big Endian number 16 encrypted bytes being: A
second random number, generated by the Wand. The response from the wand
can be verified if the decrypted random number from the response to
Challenge Part 1 is indeed the increment of the sent random number. The
second message's payload: 16 Bytes of IV for this AES-CBC packet 16
encrypted bytes being: The second random number (i.e. the one generated
by the wand), incremented by one as if it was a 128-bit Unsigned Big
Endian number This allows wand to verify iDART by checking if the value
received in Challenge Part 2 is indeed the increment of the second sent
random number. The session (or ephemeral) key would then be the second
incremented random number. The starting counter value would be the first
incremented random number. Note that starting values of all 0 for the
key and counter (pre-increment) are not permissible and will be
rejected. Values of all 0 for the counter should the counter overflow at
some point during the session are permissible. The wand either does not
response to messages outside of the Challenge IDs, or sends NACK or NOT
AUTH. Completing this handshake moves from security level 0 to level 1
and allows general communications. The key and counter value are used
until the security returns to level 0 by any mechanism (USB cable
disconnect, Bluetooth disconnect, Timeout or, Drop Session Message) The
handshake to level 2 will use the same existing mechanism from sections
3.4.1 through 3.4.4, using the level 2 device key.

Form: BF010 Version: 3 Date: 12/12/2019

Page 67 of 109

Serial API Specification

3.4.5.1 Challenge Part 1

Serial Command cmd (uint16), IV (16 bytes), Encrypted Random number (16
bytes),

cmd

0x7A10

IV

Initialisation Vector for the AES-CBC encrypted part of

the packet

Encrypted

AES-CBC encrypted (IV, level 1 device key) random

Random

number

Number

Response Description Security Level Example

acknowledge (uint8), IV (16 Bytes), Encrypted Incremented Random

Number (16 bytes), Encrypted Second Random Number(16 bytes)

acknowledge 0x06: ACK byte if successful

IV

Initialisation Vector for the AES-CBC encrypted part of

the packet

Encrypted

The random number from the command, decrypted,

Incremented incremented, and re-encrypted (AES-CBC, IV, level 1

Random

device key).

Number

Encrypted

A second random number, continuing the AES-CBC

Second

encryption

Random

Number

This command is part of the level 0 to level 1 handshake, responsible
for generating a session key and starting counter value. Level 0 Byte
Stream:

0x7A, 0x10, \[IV\] \[ENC RANDOM DATA\]

The response would be 0x06 \[IV\] \[ENC RANDOM DATA + 1\] \[ENC SECOND
RANDOM DATA\]

Form: BF010 Version: 3 Date: 12/12/2019

Page 68 of 109

Serial API Specification

3.4.5.2 Challenge Part 2

Serial Command cmd (uint16), IV (16 bytes), Encrypted Incremented Second
Random

number + 1(16 bytes)

cmd

0x7A11

IV

Initialisation Vector for the AES-CBC encrypted part of

the packet

Encrypted

The second random number from the previous

Incremented command, decrypted, incremented, and re-encrypted

Second

(AES-CBC, IV, level 1 device key).

Random

Number

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description Security Level Example

This command is part of the level 0 to level 1 handshake, responsible
for generating a session key and starting counter value. Level 0 Byte
Stream:

0x7A, 0x11, \[IV\] \[ENC SECOND RANDOM DATA + 1\]

The response would be 0x06

Encryption Each sent frame payload (minus headers and CRC) would be
encrypted using AES-CTR with the 128-bit ephemeral key and initial
counter value (which start as the incremented random numbers from the
handshake). The CRC is calculated after encryption.

For every 16 bytes of payload in a given frame the counter shall be
incremented by one as if it was a 128-bit Unsigned Big Endian number,
wrapping after 0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF.

If a frame is not a clean multiple of 16 bytes, all excess data
generated from a given counter value is discarded (consecutive frames
must not share an AES block).

If a message is 12 bytes, then the last 4 bytes of the used counter
value are discarded. If a message is 20 bytes, the first 16 bytes are
fully used, then the first 4 bytes of the next counter are used and the
other 12 discarded.

Form: BF010 Version: 3 Date: 12/12/2019

Page 69 of 109

Serial API Specification

End Session

Serial Command cmd (uint16), target level (uint8)

cmd

0x7A20

target level 0 for level 0, 1 for level 1. All other values are invalid

and are treated as level 0.

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successfully reached
target (including if the device is already at level 1 and is targeting
level 1). A NACK also indicates the end of the session by returning to
level 0.

Description Security Level Example

This command lowers the security level to the given level. This
terminates any ongoing session if level 0 is chosen. Returning to
security level 0 will also terminate a Bluetooth connection. Level 1
Byte Stream: 0x7A, 0x20, 0x00 The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 70 of 109

Serial API Specification

Level 2 Handshake Note that at any given time, only either messages
3.4.1-3.4.4 or messages 3.4.8.1-3.4.8.2 are accepted. It is the
intention that 3.4.8.1-3.4.8.2 fully replace 3.4.1-3.4.4.

Unlike the random numbers in the Level 1 Handshake, these random numbers
are not meaningful other than to determine trust, and are discarded
after the handshake.

3.4.8.1 Level 2 Challenge Part 1

Serial Command cmd (uint16), IV (16 bytes), Encrypted Random number (16
bytes),

cmd

0x7A08

IV

Initialisation Vector for the AES-CBC encrypted part of

the packet

Encrypted

AES-CBC encrypted (IV, level 2 device key) random

Random

number

Number

Response Description Security Level Example

acknowledge (uint8), IV (16 Bytes), Encrypted Incremented Random

Number (16 bytes), Encrypted Second Random Number(16 bytes)

acknowledge 0x06: ACK byte if successful

IV

Initialisation Vector for the AES-CBC encrypted part of

the packet

Encrypted

The random number from the command, decrypted,

Incremented incremented, and re-encrypted (AES-CBC, IV, level 2

Random

device key).

Number

Encrypted

A second random number, continuing the AES-CBC

Second

encryption

Random

Number

This command is part of the level 1 to level 2 handshake. Level 1 Only
Byte Stream:

0x7A, 0x08, \[IV\] \[ENC RANDOM DATA\]

The response would be 0x06 \[IV\] \[ENC RANDOM DATA + 1\] \[ENC SECOND
RANDOM DATA\]

Form: BF010 Version: 3 Date: 12/12/2019

Page 71 of 109

Serial API Specification

3.4.8.2 Level 2 Challenge Part 2

Serial Command cmd (uint16), IV (16 bytes), Encrypted Incremented Second
Random

number + 1(16 bytes)

cmd

0x7A09

IV

Initialisation Vector for the AES-CBC encrypted part of

the packet

Encrypted

The second random number from the previous

Incremented command, decrypted, incremented, and re-encrypted

Second

(AES-CBC, IV, level 2 device key).

Random

Number

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description Security Level Example

This command is part of the level 1 to level 2 handshake. Level 1 Only
Byte Stream: 0x7A, 0x09, \[IV\] \[ENC SECOND RANDOM DATA + 1\] The
response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 72 of 109

Serial API Specification

3.5 Miscellaneous KeepAlive Serial Command cmd (short) cmd

0xFFF9

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The KeepAlive command returns an ACK. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xFF, 0xF9

Get Battery % Serial Command cmd (short) cmd

0xFFF4

Response

acknowledge (byte) , percentage (uint8) acknowledge 0x06: ACK byte if
successful percentage the percentage. 100 is full, 0 is empty

Description Security Level Example

The Get Battery % command returns the battery percentage. 100 represents
fully charged, 0 represents empty. Level 1 Byte Stream: cmd(MSB),
cmd(LSB) 0xFF, 0xF4

Form: BF010 Version: 3 Date: 12/12/2019

Page 73 of 109

Serial API Specification

SaveToDiskNow Serial Command cmd (short) cmd

0xAA0D

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The SaveToDiskNow command saves all parameter and dynamic table
information to eMMC upon reception of the command, as an alternative to
waiting until shutdown for this to occur. Level 2 Byte Stream: cmd(MSB),
cmd(LSB) 0xAA, 0x0D

Get System Reset Status

Serial Command cmd (short)

cmd

0xFFFD

Response

acknowledge (byte), status (uint8)

acknowledge 0x06: ACK byte if successful

status

byte: 0 if reset not complete (i.e. not safe to write),

non-0 otherwise

Description Security Level Example

The Get System Reset Status command retrieves whether or not a reset has
completed as far as it is safe to start sending commands to change
parameters, dynamic tables, users or subscriptions. The status value is
0 if reset not complete (i.e. not safe to write), non-0 otherwise Level
1 Byte Stream: cmd(MSB), cmd(LSB) 0xFF, 0xFD

Form: BF010 Version: 3 Date: 12/12/2019

Page 74 of 109

Serial API Specification

Reset Fuel Gauge Serial Command cmd (short) cmd

0xAA0F

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Reset Fuel Gauge command issues the reset command to the fuel gauge
to perform a recalculation of current charge and zero point. Note that
this causes the board to power off unless the board is still connected
to USB. Level 2 Byte Stream: cmd(MSB), cmd(LSB) 0xAA, 0x0F

USB Slowdown

Serial Command cmd (uint16), enable(uint8)

cmd

0xAA20

enable

1 to enable USB slowdown

0 to disable USB slowdown

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description Security Level Example

This command is used to enable or disable USB Slowdown. If enable is set
to a value other than 1 or 0 a NACK is returned. This is not persistent
and the device will always boot with USB slowdown disabled. Level 2 Byte
Stream: cmd(MSB), cmd(LSB), enable

0xAA, 0x20, 0x01

The response would be 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 75 of 109

Serial API Specification

Get System Logs Serial Command cmd (short) cmd

0xFFFF

Response

acknowledge (byte), logs (string)

acknowledge 0x06: ACK byte if successful

logs

non-zero terminated string containing all system logs

since either boot or the most recent time this

command was sent, whichever was most recent.

Description Security Level Example

The GetSystemLogs command (if enabled in the given build) returns all
logs that have yet to be returned by this command, up to the system's
log limit of 64kB. This string is not necessarily null-terminated and
the frame length should be used to determine string length. Level 1 Byte
Stream: cmd(MSB), cmd(LSB)

0xFF, 0xFF

Get Fuel Gauge Temperature

Serial Command cmd (short)

cmd

0xAA21

Response

acknowledge (byte), external temperature (int16), internal

temperature (int16)

acknowledge 0x06: ACK byte if successful

external

The thermistor temperature in 0.1 Kelvin

temperature

internal

The fuel gauge package temperature in 0.1 Kelvin

temperature

Description Security Level Example

The GetFuelGaugeTemperature command returns both the temperature given
by the thermistor by the batteries and the fuel gauge's internal
temperature. These values are updated approximately every 10 seconds.
Level 2 Byte Stream: cmd(MSB), cmd(LSB) 0xAA, 0x21

Form: BF010 Version: 3 Date: 12/12/2019

Page 76 of 109

Serial API Specification

3.6 Bluetooth Get Bluetooth RSSI Serial Command cmd (short) cmd

0xFFF6

Response

acknowledge (byte) , rssi (int8) acknowledge 0x06: ACK byte if
successful percentage The RSSI. 127 is invalid

Description Security Level Example

The Get Bluetooth RSSI command returns the current bluetooth
connection's RSSI (if valid). Received signal strength in dB relative to
'Golden Receive Power Range' which is from -40 dBm to -60 dBm. Negative
values indicate we are below minimum of the range and positive values
indicate that we are above the maximum of the range Level 1 Byte Stream:
cmd(MSB), cmd(LSB) 0xFF, 0xF6

Get Bluetooth Enabled Serial Command cmd (short) cmd

0xAA0A

Response

acknowledge (byte) , enabled (bool)

acknowledge 0x06: ACK byte if successful

enabled

1 if Bluetooth advertising is enabled

0 if Bluetooth advertising is disabled

Description Security Level Example

The Get Bluetooth Enabled command returns a Boolean value to determine
whether the bluetooth module is currently advertising it's presence. Is
set per active subscription. Level 2 Byte Stream: cmd(MSB), cmd(LSB)
0xAA, 0x0A

Form: BF010 Version: 3 Date: 12/12/2019

Page 77 of 109

Serial API Specification

Set Bluetooth Enabled

Serial Command cmd (uint16), enable(uint8)

cmd

0xAA0B

enable

1 to enable Bluetooth Advertising

0 to disable Bluetooth Advertising

Response

acknowledge (uint8) acknowledge 0x06: ACK byte if successful

Description Security Level Example

This command is used to enable or disable Bluetooth Advertising. If
enable is set to a value other than 1 or 0 a NACK is returned. Is set
per active subscription. Level 2 Byte Stream: cmd(MSB), cmd(LSB), enable

0xAA, 0x0B, 0x01

The response would be 0x06

Clear Bluetooth Pairings

Serial Command cmd (short)

cmd

0xAA0C

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Clear Bluetooth Pairings command clears the pairing database on the
Bluetooth module. Level 2 Byte Stream: cmd(MSB), cmd(LSB) 0xAA, 0x0C

Form: BF010 Version: 3 Date: 12/12/2019

Page 78 of 109

Serial API Specification

Get Bluetooth Address Serial Command cmd (short) cmd

0xAA1C

Response

acknowledge (byte), bdaddr (6 bytes)

acknowledge 0x06: ACK byte if successful

bdaddr

6 bytes representing the Bluetooth address, LE.

Description Security Level Example

The Get Bluetooth Address command retrieves the Bluetooth address in use
by the Bluetooth module. Level 2 Byte Stream: cmd(MSB), cmd(LSB) 0xAA,
0x1C

Form: BF010 Version: 3 Date: 12/12/2019

Page 79 of 109

Serial API Specification

3.7 Subscription

Add Subscription

Serial Command cmd (short), guid (uint8\[16\]), start date (uint32),
start time(uint32),

expiry date(uint32), expiry time(uint32), sync date (uint32), sync time

(uint32), reserved data (16 bytes)

cmd

0xF010

guid

a 16-byte GUID

start date

A uint32 representing the start date of the

subscription, as per SetDateTime

start time

A uint32 representing the start time of the

subscription, as per SetDateTime

expiry date A uint32 representing the expiry date of the

subscription, as per SetDateTime

expiry time A uint32 representing the expiry time of the

subscription, as per SetDateTime

sync date

A uint32 representing the sync date of the subscription,

as per SetDateTime

sync time

A uint32 representing the sync time of the

subscription, as per SetDateTime

reserved data 16 bytes of empty data, reserved for future use

Response

acknowledge (byte), index (byte)

acknowledge 0x06: ACK byte if successful

index

uint8: representing which index the sub was added to.

0xFF means not added.

Description Security Level Example

The Add Subscription command adds the subscription information to the
device tables. If this command returns NACK for any reason (invalid
data, subscription has already expired, subscription starts in the
future, subscription not 3-12 months long, too many subscriptions (max.
8)), it will not modify the table. It is expected the date and time will
be set appropriately before sending subscription information. A start
time, start date, expiry time and expiry date all of 0xABABABAB will be
treated as a non-expiring subscription that does not alert the user to
subscriptions. This does not actually set an active subscription. Level
2 Byte Stream: cmd(MSB), cmd(LSB) 0xF0, 0x10

Form: BF010 Version: 3 Date: 12/12/2019

Page 80 of 109

Serial API Specification

Set Active Subscription

Serial Command cmd (short), index(uint8)

cmd

0xF012

index

Uint8 representing the index. A value of 0xFF indicates

no active subscription set

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Set Active Subscription command sets the given index as the active
subscription, assuming it has already been added. If this command fails
(returns a NACK for any reason), no changes will take place. This will
cause the device to reset back to the sign-in page if the table was
changed from the current value, and no further configuration should take
place until this process completes (See GetSystemResetStatus). A
subscription must be set active in order for the Wand v3 to work Level 2
Byte Stream: cmd(MSB), cmd(LSB) 0xF0, 0x12

Get Active Subscription Serial Command cmd (short) cmd

0xF013

Response

acknowledge (byte), index(uint8)

acknowledge 0x06: ACK byte if successful

index

Uint8 representing the index. A value of 0xFF indicates

no active subscription set

Description Security Level Example

The Get Active Subscription command gets the given index of the active
subscription. A value of 0xFF indicates no active subscription set.
Level 2 Byte Stream: cmd(MSB), cmd(LSB) 0xF0, 0x13

Form: BF010 Version: 3 Date: 12/12/2019

Page 81 of 109

Serial API Specification

Delete Subscriptions Serial Command cmd (short) cmd

0xF014

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Delete Subscriptions command removes any and all subscription
information from the device along with any saved dynamic tables, users,
parameters and so on. This will cause the device to reset back to the
sign-in page, and no further configuration should take place until this
process completes (See GetSystemResetStatus). It is highly recommended
to first set the active subscription to No Active Subscription (0xFF)
before performing this action. In addition, this command is
asynchronous, if this command returns ACK, Subscription Delete Status
should subsequently by called (every 250ms) until it returns non-0
before further subscription messages are sent. Level 2 Byte Stream:
cmd(MSB), cmd(LSB) 0xF0, 0x14

Form: BF010 Version: 3 Date: 12/12/2019

Page 82 of 109

Serial API Specification

Get Subscription At Index

Serial Command cmd (short) , index (uint8)

cmd

0xF017

index

A uint8 representing the index to retrieve

Response

acknowledge (byte), guid (uint8\[16\]), start date (uint32), start

time(uint32), expiry date(uint32), expiry time(uint32), sync date

(uint32), sync time (uint32), reserved data (16 bytes)

acknowledge 0x06: ACK byte if successful

guid

a 16-byte GUID

start date

A uint32 representing the start date of the

subscription, as per SetDateTime

start time

A uint32 representing the start time of the

subscription, as per SetDateTime

expiry date A uint32 representing the expiry date of the

subscription, as per SetDateTime

expiry time A uint32 representing the expiry time of the

subscription, as per SetDateTime

sync date

A uint32 representing the sync date of the subscription,

as per SetDateTime

sync time

A uint32 representing the sync time of the

subscription, as per SetDateTime

reserved data 16 bytes of empty data, reserved for future use

Description Security Level Example

The Get Subscription command retrieves any subscription information from
the device. An invalid subscription is represented by a GUID of all 0.
Level 2 Byte Stream: cmd(MSB), cmd(LSB) 0xF0, 0x17

Form: BF010 Version: 3 Date: 12/12/2019

Page 83 of 109

Serial API Specification

Replace Subscription At Index

Serial Command cmd (short) , guid (uint8\[16\]), start date (uint32),
start time(uint32),

expiry date(uint32), expiry time(uint32), sync date (uint32), sync time

(uint32), reserved data (16 bytes), index (uint8)

cmd

0xF018

guid

a 16-byte GUID

start date

A uint32 representing the start date of the

subscription, as per SetDateTime

start time

A uint32 representing the start time of the

subscription, as per SetDateTime

expiry date A uint32 representing the expiry date of the

subscription, as per SetDateTime

expiry time A uint32 representing the expiry time of the

subscription, as per SetDateTime

sync date

A uint32 representing the sync date of the subscription,

as per SetDateTime

sync time

A uint32 representing the sync time of the

subscription, as per SetDateTime

reserved data 16 bytes of empty data, reserved for future use

index

A uint8 representing the index to retrieve

Response

acknowledge (byte), acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Replace Subscription At Index command replaces any subscription
information from the device for the given index. If the given GUID
matches the replaced GUID, only the date/times are updated and the rest
of the description does not apply (i.e. nothing is deleted or reset when
updating a subscription's dates/times). If the target subscription does
not exist, the given subscription will still be added. A start time,
start date, expiry time and expiry date all of 0xABABABAB will be
treated as a non-expiring subscription that does not alert the user to
subscriptions. This command will cause the device to reset back to the
sign-in page if the current subscription is replaced with a new GUID.
This command will also cause all parameters, users, dynamic table etc.
to be reset to default for the given subscription and no further
configuration should take place until this process completes (See Get
System Reset Status). If replacing a subscription with a different GUID
it is highly recommended to first set the active subscription to a
different subscription or No Active Subscription (0xFF). In addition,
this command is asynchronous, if this command returns ACK, Subscription
Delete Status should subsequently by called (every 250ms) until it
returns non-0 before further subscription messages are sent. Level 2
Byte Stream: cmd(MSB), cmd(LSB) 0xF0, 0x18

Form: BF010 Version: 3 Date: 12/12/2019

Page 84 of 109

Serial API Specification

Find Subscription By Guid

Serial Command cmd (short) , guid (uint8\[16\])

cmd

0xF01A

guid

a 16-byte GUID to find

Response

acknowledge (byte), index (uint8)

acknowledge 0x06: ACK byte if successful

index

uint8 representing the index of the GUID, or 0xFF if not

found

Description Security Level Example

The Find Subscription By Guid command retrieves the index of the given
GUID from the device. If the GUID is not present, the value returned is
0xFF Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xF0, 0x1A

Delete Subscription At Index

Serial Command cmd (short), index (uint8)

cmd

0xF015

index

uint8: the index of the subscription to delete.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The Delete Subscription At Index command removes the subscription
information for the given index from the device along with any saved
dynamic tables, users, parameters and so on. This will cause the device
to reset back to the sign-in page if the current subscription is
deleted, and no further configuration should take place until this
process completes (See Get System Reset Staus). It is highly recommended
to first set the active subscription to a different subscription or No
Active Subscription (0xFF). In addition, this command is asynchronous,
if this command returns ACK, Subscription Delete Status should
subsequently by called (every 250ms) until it returns non-0 before
further subscription messages are sent. Level 2 Byte Stream: cmd(MSB),
cmd(LSB) 0xF0, 0x15

Form: BF010 Version: 3 Date: 12/12/2019

Page 85 of 109

Serial API Specification

Subscription Delete Status

Serial Command cmd (short)

cmd

0xF024

Response

acknowledge (byte), status (uint8)

acknowledge 0x06: ACK byte if successful

status

uint8, 0 if there is currently a command in progress,

non-zero if there is no command currently in progress

Description Security Level Example

The Subscription Delete Status command retrieves the status of any
ongoing subscription delete operation (Delete Subscription At Index,
Delete Subscriptions, Replace Subscription At Index). 0 means an ongoing
operation, non-0 means operation complete. Level 2 Byte Stream:
cmd(MSB), cmd(LSB) 0xF0, 0x24

Form: BF010 Version: 3 Date: 12/12/2019

Page 86 of 109

Serial API Specification

3.8 User Management

Several of these commands have been changed from Wand v2 (RFID has been
replaced with

GUID). All user commands are relative to the active subscription

GetFirstUser

Serial Command cmd (short)

cmd

0xF001

Response

acknowledge (byte), guid (string), username (string), pin (string),

company (string), site (string)

acknowledge 0x06: ACK byte if successful

guid

A 16-byte sequence.

username

A 16-character string, pad unused characters with

zeros.

pin

A 4-character string, numbers only.

company

A 32-character string, pad unused characters with

zeros.

site

A 32-character string, pad unused characters with

zeros.

Description Security Level Example

The GetFirstUser command returns the first user entry in the WAND 3.0
user database. Use this command to start getting a list of users that
are installed in the database. See also command GetNextUser. Note, of
there are no users installed, the command will return NAK (0x21) Level 2
Byte Stream: cmd(MSB), cmd(LSB)

0xF0, 0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 87 of 109

Serial API Specification

GetNextUser Serial Command cmd (short) cmd

0xF002

Response

acknowledge (byte), guid (string), username (string), pin (string),

company (string), site (string)

acknowledge 0x06: ACK byte if successful

guid

A 16-byte sequence.

username

A 16-character string, pad unused characters with

zeros.

pin

A 4-character string, numbers only.

company

A 32-character string, pad unused characters with

zeros.

site

A 32-character string, pad unused characters with

zeros.

Description Security Level Example

The GetNextUser command returns the next user entry in the WAND 3.0 user
database. Use this command to get a list of users that are installed in
the database. See also command GetFirstUser. Note if you have reached
the end of the list, this command returns NAK (0x21) Level 2 Byte
Stream: cmd(MSB), cmd(LSB)

0xF0, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 88 of 109

Serial API Specification

AddUser Serial Command

cmd (short), guid (string), username (string), pin (string), company

(string), site (string)

cmd

0xF003

guid

A 16-byte sequence.

username

A 16-character string, pad unused characters with

zeros.

pin

A 4-character string, numbers only.

company

A 32-character string, pad unused characters with

zeros.

site

A 32-character string, pad unused characters with

zeros.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The AddUser command adds a user to the WAND 3.0 user database. If the
operation is successful, ACK is returned, else a NAK (0x21) where the
operation fails. Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xF0, 0x03

DeleteUser

Serial Command cmd (short), username (string)

cmd

0xF004

username

A 16-character string, pad unused characters with

zeros.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The DeleteUser command deletes a user from the WAND 3.0 user database.
If the operation is successful, ACK is returned, else a NAK (0x21) where
the operation fails. Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xF0, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 89 of 109

Serial API Specification

DeleteAllUsers Serial Command cmd (short) cmd

0xF005

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The DeleteAllUsers command deletes all users from the WAND 3.0 user
database. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 2 Byte Stream: cmd(MSB),
cmd(LSB)

0xF0, 0x05

Form: BF010 Version: 3 Date: 12/12/2019

Page 90 of 109

Serial API Specification

3.9 From Wand v2

Note that any Wand v2 message IDs that do not appear here are either
obsolete or have

been modified for Wand v3 and appear earlier in this document.

All commands except DoScan, GetCurrentFileHdr and GetCurrentFile are
relative to the

active subscription.

Sensor Management

3.9.1.1 DeleteSensor

Serial Command cmd (short), rfid (string)

cmd

0xF104

rfid

A 12-byte sequence.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The DeleteSensor command deletes a sensor from the WAND 2.0 location
database. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. Level 1 Byte Stream: cmd(MSB),
cmd(LSB)

0xF1, 0x04

Measurements

These commands relate to managing measurement datasets on the connected
WAND 2.0

3.9.2.1 GetFirstMeasurement

Serial Command cmd (short)

cmd

0xF201

Response

acknowledge (byte), measurement data acknowledge 0x06: ACK byte if
successful measurement The measurement data as per "fileformat_schemaX"
data

Description Security Level Example

The GetFirstMeasurement command returns the first measurement in the
WAND 2.0 file system. Use this command to start getting all measurement
data from a connected wand. See also command GetNextMeasurment. Note, if
there are measurements available, the command will return NAK (0x21)
Level 1 Byte Stream: cmd(MSB), cmd(LSB)

0xF2, 0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 91 of 109

Serial API Specification

3.9.2.2 GetNextMeasurement Serial Command cmd (short) cmd

0xF202

Response

acknowledge (byte), measurement data acknowledge 0x06: ACK byte if
successful measurement The measurement data as per "fileformat_schemaX"
data

Description Security Level Example

The GetNextMeasurement command returns the next measurement in the WAND
2.0 filesystem. Use this command to get all the measurements from the
WAND 2.0. See also command GetNextMeasurement. Note if you have reached
the end of the available measurements, this command returns NAK (0x21)
Level 1 Byte Stream: cmd(MSB), cmd(LSB)

0xF2, 0x02

3.9.2.3 DeleteAllMeasurements

Serial Command cmd (short)

cmd

0xF204

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The DeleteAllMeasurements command deletes all the measurements on the
WAND 2.0 filesystem. If the operation is successful, ACK is returned,
else a NAK (0x21) where the operation fails. In addition, this command
is asynchronous, if this command returns ACK, MeasurementDeleteStatus
should subsequently by called (every 250ms) until it returns non-0
before further subscription messages are sent. Level 1 Byte Stream:
cmd(MSB), cmd(LSB)

0xF2, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 92 of 109

Serial API Specification

3.9.2.4 GetNumMeasurements Serial Command cmd (short) cmd

0xF205

Response

acknowledge (byte) number (uint32_t)

acknowledge 0x06: ACK byte if successful

number

The number of measurements on the file system (4-

byte uint32_t)

Description Security Level Example

The GetNumMeasurements command returns the number of measurements on the
WAND 2.0 filesystem. If the operation is successful, ACK is returned,
else a NAK (0x21) where the operation fails. Level 1 Byte Stream:
cmd(MSB), cmd(LSB)

0xF2, 0x05

3.9.2.5 GetMeasurementAtIndex

Serial Command cmd (short) index (uint32_t)

cmd

0xF206

Response

acknowledge (byte), measurement data acknowledge 0x06: ACK byte if
successful measurement The measurement data as per "fileformat_schemaX"
data

Description Security Level Example

The GetMeasurementAtIndex command returns the measurement data from the
requested index on the WAND 2.0 filesystem. If the operation is
successful, ACK is returned, else a NAK (0x21) where the operation
fails. Level 1 Byte Stream: cmd(MSB), cmd(LSB)

0xF2, 0x06

Form: BF010 Version: 3 Date: 12/12/2019

Page 93 of 109

Serial API Specification

3.9.2.6 DeleteAllMeasurementsStatus

Serial Command cmd (short)

cmd

0xF214

Response

acknowledge (byte), status (uint8)

acknowledge 0x06: ACK byte if successful

status

uint8, 0 if there is currently a command in progress,

non-zero if there is no command currently in progress

Description Security Level Example

The DeleteAllMeasurementsStatus command retrieves the status of any
ongoing measurements delete operation. 0 means an ongoing operation,
non-0 means operation complete. Level 1 Byte Stream: cmd(MSB), cmd(LSB)
0xF2, 0x14

Form: BF010 Version: 3 Date: 12/12/2019

Page 94 of 109

Serial API Specification

High Temperature operation.

These commands relate to managing high temperature on the connected WAND
2.0.

3.9.3.1 GetHighTempParam

Serial Command cmd (short)

cmd

0xF401

Response

acknowledge (byte)

acknowledge enable lin_coeff_alpha lin_coeff_beta thermal_expan
compen_factor td_cal threshold

0x06: ACK byte if successful (int) 0x00000001 = Enabled, 0x00000000 =
Disabled (float) Linear Approximation Coefficient Alpha (float) Linear
Approximation Coefficient Beta (float) Thermal expansion coefficient of
the delay line (float) Compensation factor per centigrade (float) Td
calibration value. (float) threshold for peak detection

Description Security Level Example

The GetHighTempParam returns the high temperature parameters from Wand
2.0 Level 1 Byte Stream: cmd(MSB), cmd(LSB)

0xF4, 0x01

3.9.3.2 SetHighTempParam

Serial Command cmd (short), enable (int), lin_coeff_alpha (float),
lin_coeff_beta (float),

thermal_expan (float), compen_factor (float), td_cal (float), threshold

(float)

cmd

0xF402

enable

(int) 0x00000001 = Enable, 0x00000000 = Disable

lin_coeff_alpha (float) Linear Approximation Coefficient Alpha

lin_coeff_beta (float) Linear Approximation Coefficient Beta

thermal_expan (float) Thermal expansion coefficient of the delay line

compen_factor (float) Compensation factor per centigrade

td_cal

(float) Td calibration value.

threshold

(float) Threshold for peak detection.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The SetHighTempParam sets the high temperature parameters in Wand 2.0
Level 1 Byte Stream: cmd(MSB), cmd(LSB)

0xF4, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 95 of 109

Serial API Specification

System Delay

These commands relate to managing system delay on the connected WAND
2.0.

3.9.4.1 GetSystemDelay

Serial Command cmd (short)

cmd

0xF501

Response

acknowledge (byte), system_delay (float) acknowledge 0x06: ACK byte if
successful system_delay (float) The system delay time set in seconds.

Description Security Level Example

The GetSystemDelay returns the system delay setting from Wand 2.0 Level
1 Byte Stream: cmd(MSB), cmd(LSB)

0xF5, 0x01

3.9.4.2 SetSystemDelay

Serial Command cmd (short), system_delay (float)

cmd

0xF502

system_delay (float) The system delay to set in the wand in seconds.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The SetSystemDelay sets the System Delay time in Wand 2.0 Level 1 Byte
Stream: cmd(MSB), cmd(LSB)

0xF5, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 96 of 109

Serial API Specification

Wand Settings.

These commands relate to managing settings on the connected WAND 2.0.

3.9.5.1 GetShutdownTime

Serial Command cmd (short)

cmd

0xF601

Response

acknowledge (byte), shutdown_time (short)

acknowledge shutdown_time

0x06: ACK byte if successful (short) An index that indicates what the
shutdown time is set to: 0 = 15 Seconds 1 = 30 Seconds 2 = 1 Minute 3 =
2 Minutes 4 = 3 Minutes 5 = 4 Minutes 6 = 5 Minutes 7 = Never

Description Security Level Example

The GetShutdownTime returns the shutdown time setting from Wand 2.0
Level 1 Byte Stream: cmd(MSB), cmd(LSB)

0xF6, 0x01

Form: BF010 Version: 3 Date: 12/12/2019

Page 97 of 109

Serial API Specification

3.9.5.2 SetShutdownTime

Serial Command cmd (short), shutdown_time (float)

cmd

0xF602

shutdown_time (short) The shutdown time index to set in the wand

according to the following table:

0 = 15 Seconds

1 = 30 Seconds

2 = 1 Minute

3 = 2 Minutes

4 = 3 Minutes

5 = 4 Minutes

6 = 5 Minutes

7 = Never

Response

acknowledge (byte)

acknowledge

0x06: ACK byte if successful or NACK if the index is invalid

Description Security Level Example

The SetShutdownTime sets the shutdown time index in Wand 2.0 Level 1
Byte Stream: cmd(MSB), cmd(LSB)

0xF6, 0x02

3.9.5.3 GetRfidEnable Serial Command cmd (short) cmd

0xF603

Response

acknowledge (byte), rfid_enable (short)

acknowledge rfid_enable

0x06: ACK byte if successful (short) The value of the RFID Enable.
0x0001 = Enabled. 0x0000 = disabled.

Description Security Level Example

The GetRfidEnable returns the RFID Enable setting from Wand 2.0 Level 1
Byte Stream: cmd(MSB), cmd(LSB)

0xF6, 0x03

Form: BF010 Version: 3 Date: 12/12/2019

Page 98 of 109

Serial API Specification

3.9.5.4 SetRfidEnable

Serial Command cmd (short), rfid_enable (short)

cmd

0xF604

rfid_enable

(short) The RFID Enable value to set in the wand,

0x0001 = Enable, 0x0000 = Disable.

Response

acknowledge (byte)

acknowledge

0x06: ACK byte if successful or NACK if the index is invalid

Description Security Level Example

The SetRfidEnable sets the RFID Enable parameter in Wand 2.0 Level 1
Byte Stream: cmd(MSB), cmd(LSB)

0xF6, 0x04

3.9.5.5 GetVideoIndex Serial Command cmd (short) cmd

0xF605

Response

acknowledge (byte), video_index (short)

acknowledge video_index

0x06: ACK byte if successful (short) The value of the video index. The
following values are valid: 0 = Standard 1 = TRND 2 = China Shipbuilding

Description Security Level Example

The GetVideoIndex returns the video index setting from Wand 2.0 Level 1
Byte Stream: cmd(MSB), cmd(LSB)

0xF6, 0x05

Form: BF010 Version: 3 Date: 12/12/2019

Page 99 of 109

Serial API Specification

3.9.5.6 SetVideoIndex

Serial Command cmd (short), video_index (short)

cmd

0xF606

video_index (short) The video index setting for the startup video.

Valid values are:

0 = Standard

1 = TRND

2 = China Shipbuilding

Response

acknowledge (byte)

acknowledge

0x06: ACK byte if successful or NACK if the index is invalid

Description Security Level Example

The SetVideoIndex sets the Video Index parameter in Wand 2.0 Level 1
Byte Stream: cmd(MSB), cmd(LSB)

0xF6, 0x06

3.9.5.7 GetDateTime Serial Command cmd (short) cmd

0xF607

Response

acknowledge (byte), date (word), time (word)

acknowledge date time

0x06: ACK byte if successful

(word) Date in same format as file header:

Byte

Description

3-2

Year

1

Month

0

Day

(word) Time in the same format as file header:

Byte

Description

3-2

Hour

1

Minute

0

Seconds

Description Security Level Example

The GetDateTime returns the current date and time from Wand 2.0 Level 2
Byte Stream: cmd(MSB), cmd(LSB)

0xF6, 0x07

Form: BF010 Version: 3 Date: 12/12/2019

Page 100 of 109

Serial API Specification

3.9.5.8 SetDateTime

Serial Command cmd (short), date (word), time (word)

cmd

0xF608

date

(word) Date in same format as file header:

Byte

Description

3-2

Year

1

Month

0

Day

time

(word) Time in the same format as file header:

Byte

Description

3-2

Hour

1

Minute

0

Seconds

Response

acknowledge (byte)

acknowledge

0x06: ACK byte if successful or NACK if the date or time is invalid

Description Security Level Example

The SetDateTime sets the date and time in Wand 2.0. Level 2 Byte Stream:
cmd(MSB), cmd(LSB)

0xF6, 0x08

Form: BF010 Version: 3 Date: 12/12/2019

Page 101 of 109

Serial API Specification

Engineering Commands

These commands are for production and testing purposes and should not be
used for any

other purpose. Note that some of these commands if not used cautiously
can damage the

wand hardware.

3.9.6.1 GetRfidPower

Serial Command cmd (short)

cmd

0xAA01

Response

acknowledge (byte), power_status (byte) acknowledge 0x06: ACK byte if
successful power_status 0x00: Power is OFF 0x01: Power is ON

Description Security Level Example

The GetRfidPower command returns the status of the power to the RFID
module in the wand. If the operation is successful, ACK is returned,
else a NAK (0x21) where the operation fails. Level 2 Byte Stream:
cmd(MSB), cmd(LSB)

0xAA, 0x01

3.9.6.2 SetRfidPower

Serial Command cmd (short), rfid power (byte)

cmd

0xAA02

rfid power

A byte:

0x00: RFID Module Power OFF

0x01: RFID Module Power ON

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The SetRfidPower command turns the power to the RFID module in the Wand
On or OFF. If the operation is successful, ACK is returned, else a NAK
(0x21) where the operation fails. WARNING: Only use this command if you
know what you are doing. The RFID module will start to transmit and
rapidly heat up to high temperature when the powering ON. If left on for
extended time periods, this will lead to hardware damage. Level 2 Byte
Stream: cmd(MSB), cmd(LSB)

0xAA, 0x02

Form: BF010 Version: 3 Date: 12/12/2019

Page 102 of 109

Serial API Specification

3.9.6.3 DoScan Serial Command cmd (short) cmd

0xAA03

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The DoScan command is the equivalent of a user pressing the scan button
on the wand to initiate a measurement. If the operation is successful,
ACK is returned, else a NAK (0x21) where the operation fails. Note that
the Wand must be displaying the measure screen for this command to work
successfully. As of Firmware version 3.7, this will now save a valid
reading when an existing location matches the detected RFID tag. If RFID
is disabled, or no tag is detected, or a location does not exist for a
detected RFID tag, the measurement will not be saved. Level 2 Byte
Stream: cmd(MSB), cmd(LSB)

0xAA, 0x03

3.9.6.4 GetCurrentFileHdr Serial Command cmd (short) cmd

0xAA04

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful file header
See the Schema definitions: Schema1: 52 Bytes Schema2: 136 Bytes
Schema4: 144 Bytes Schema5: 224 Bytes Schema6: 144 Bytes Schema7: 144
Bytes + 40000 Data Bytes

Description Security Level Example

The GetCurrentFileHdr command returns the metadata header for the last
successful measurement done in the Wand 3.0. The size of the header
depends on the file schema supported by the firmware. If the operation
is successful, ACK is returned, else a NAK (0x21) where the operation
fails. Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x04

Form: BF010 Version: 3 Date: 12/12/2019

Page 103 of 109

Serial API Specification

3.9.6.5 GetCurrentFile Serial Command cmd (short) cmd

0xAA05

Response

acknowledge (byte)

acknowledge 0x06: ACK byte if successful

file

See the Schema definitions:

Schema1: 52 Bytes + 40000 Data Bytes

Schema2: 136 Bytes + 40000 Data Bytes

Schema4: 144 Bytes + 40000 Data Bytes

Schema5: 224 Bytes + 40000 Data Bytes

Schema6: 144 Bytes + 40000 Data Bytes

Schema7: 144 Bytes + 40000 Data Bytes

Description Security Level Example

The GetCurrentFile command returns the metadata header and the data for
the last successful measurement done in the Wand 3.0. The size of the
header depends on the file schema supported by the firmware. If the
operation is successful, ACK is returned, else a NAK (0x21) where the
operation fails. Level 2 Byte Stream: cmd(MSB), cmd(LSB)

0xAA, 0x05

Misc Commands 3.9.7.1 Get Information

Serial Command cmd (short) cmd

0xFFF0

Response

acknowledge (byte), serial_number (short), major_version (byte),
minor_version (byte) acknowledge 0x06: ACK byte Serial_number (short)
Serial number of the device Major_version (byte) major version number of
the firmware Minor_version (byte) minor version number of the firmware

Description Security Level Example

The Get Information command returns the serial number and the firmware
version of the wand. ACK is always returned. Level 1 Byte Stream:
cmd(MSB), cmd(LSB)

0xFF, 0xF0

Form: BF010 Version: 3 Date: 12/12/2019

Page 104 of 109

Serial API Specification

3.10 Provisioning

It is not yet currently recommended to use these commands unless
permanence is desired ­

there is no convenient method of de-provisioning a device.

ProvisionDeviceKey

Serial Command cmd (short), key 1 (16-byte array), key 2 (16 byte array)

cmd

0x55A0

key 1

16 bytes representing the level 1private key

key 2

16 bytes representing the level 2 private key

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ProvisionDeviceKey saves the device private keys (used for
authentication), assuming the private keys are not already present on
the device. The device will then need restarting to make use of these
keys. Private keys of all-zero are not valid. Level 0 - TBD Byte Stream:
cmd(MSB), cmd(LSB) 0x55, 0xA0

ProvisionDeviceSerialNo

Serial Command cmd (short), serialNo (uint16)

cmd

0x55A1

serialNo

uint16 representing the serial number of the device.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The ProvisionDeviceSerialNo saves the device serial number, assuming a
serial number is not already present on the device. The device will then
need restarting to make use of this. A serial number of 0 is not valid.
Level 0 - TBD Byte Stream: cmd(MSB), cmd(LSB) 0x55, 0xA1

Form: BF010 Version: 3 Date: 12/12/2019

Page 105 of 109

Serial API Specification

Remove Provisioning Serial Command cmd (short), cmd

0x55AF

Response

acknowledge (byte) acknowledge 0x06: ACK byte if successful

Description Security Level Example

The RemoveProvisioning command removes the serial number and device keys
from the wand if present. This command is obsolete and will not be in
firmware from version 3.12 onwards. Level 0 - TBD Byte Stream: cmd(MSB),
cmd(LSB) 0x55, 0xAF

Form: BF010 Version: 3 Date: 12/12/2019

Page 106 of 109

Serial API Specification

3.11 Production Test

Start Production Test

Serial Command cmd (short), tests (13 bytes)

cmd

0xB410

tests

A value of 1 is used to activate the test in question.

In order these tests are:

DSP DRAM

Boot Flash

USB

eMMC

Screen

RTC

USB-C chip

Fuel Gauge

Battery Charger

Cartridge EEPROM

Cartridge RFID reader

CPLD SRAM

Bluetooth

Response

acknowledge (byte) acknowledge 0x06: ACK byte if test(s) successfully
started

Description Security Level Example

The Start Production Test command starts production tests for the given
enabled tests, assuming a test isn't already in progress. Level 2 Byte
Stream: cmd(MSB), cmd(LSB) 0xB4, 0x10, 0x01, 0x01, 0x01, 0x01, 0x01,
0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01 This will start all
tests.

Form: BF010 Version: 3 Date: 12/12/2019

Page 107 of 109

Serial API Specification

Production Test Status Serial Command cmd (short) cmd

0xB411

Response Description Security Level Example

acknowledge (byte), test status (13 bytes)

acknowledge 0x06: ACK byte if test(s) successfully started

test status

Each byte is a bitfield with each test representing the

following:

0x01: Test Active.

0x02: Test Success.

0x04: Test Failure Condition 1

0x08: Test Failure Condition 2

0x10: Test Failure Condition 3

0x20: Test Failure Condition 4

0x40: Test Failure Condition 5

0x80: Test Failure Condition 6

If a test has yet to be performed, only Test Active will

be set. If a test has succeeded, Test Active and Test

Success will both be set (i.e. vale 0x03).

For the test order, see StartProductionTest.

The Start Production Test command starts production tests for the given
enabled tests, assuming a test isn't already in progress. Level 2 Byte
Stream: cmd(MSB), cmd(LSB)

0xB4, 0x11,

Most tests only use failure condition 1: Exceptions are mentioned below.
For DRAM, Test Failure Condition 1 means failure on first DDR3 bank,
Test Failure Condition 2 means failure on second DDR3 bank. For CPLD,
Test Failure Condition 1 means failure against ADC SRAM, Test Failure
Condition 2 means failure against DAC SRAM.

Form: BF010 Version: 3 Date: 12/12/2019

Page 108 of 109

Serial API Specification

Program Cartridge EEPROM

Serial Command cmd (short), cartridge IDno. (uint16 BE) cartridge serial
no. (uint32 BE),

write protect (uint8)

cmd

0xB412

cartridge ID 2 bytes representing the ID type of this cartridge, Big

Endian. Same as Cartridge ID in dynamic tables (3.3.25-

3.3.30).

cartridge

4 bytes representing the serial number of this

serial no.

cartridge, Big Endian. Cannot be 0xFFFFFFFF unless

write protect in this packet is also 0xFF and attached

cartridge is currently unprovisioned.

write protect 1 byte to set write-protection in the cartridge EEPROM.

This value must NOT be 0xFF unless cartridge serial no

in this packet is also 0xFFFFFFFF and attached cartridge

is currently unprovisioned.

Response

acknowledge (byte) acknowledge 0x06: ACK byte if test(s) successfully
started

Description Security Level Example

The Program Cartridge EEPROM command programs the attached cartridge's
EEPROM, assuming it hasn't been programmed before. This function returns
ACK if programming was successful, and NACK if programming was
unsuccessful for any reason. Level 2 Byte Stream: For a cartridge of ID
no. 1 and serial no. 2: cmd(MSB), cmd(LSB) 0xB4, 0x12, 0x00, 0x01, 0x00,
0x00, 0x00, 0x02, 0x00

Form: BF010 Version: 3 Date: 12/12/2019

Page 109 of 109


