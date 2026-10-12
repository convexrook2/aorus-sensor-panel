;=====Titles, Meters and Icons=====


[Variables]
GPUHWiNFOSensorInstance=0x0

;=====Motherboard=====

;-----MB Name-----
[CustomMBnametext]
;Custom MB name to avoid the brand name for more space.
Meter=String
FontFace=AORUS
StringAlign=Left
Text=Z790 AORUS PRO
FontColor=494949
FontSize=21
Antialias=1
X=220
Y=20


;=====Time And Date=====
[measureDate]
Measure=Time
Format=%m/%d/%Y
UpdateDivider=-1

[measureTime]
Measure=Time
Format=%H:%M %p

[DayAndDate]
Meter=String
MeasureName=measureTime
MeasureName2=measureDate
FontFace=Titillium Web Semibold
FontColor=90,90,90
FontSize=16
StringAlign=Left
Antialias=1
Text=%1   %2
X=781
Y=20


;=====CPU=====

;-----CPU Name-----
[measureCPUname]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0000300
HWiNFOSensorInstance=0x0
HWiNFOType=SensorName
RegExpSubstitute=1
Substitute="^(.*]: )":"","(: .*)$":""
UpdateDivider=-1

[CPUtitle]
Meter=String
MeasureName=measureCPUName
FontFace=AORUS
StringAlign=Left
Text=%1
FontColor=212121
FontSize=15
Antialias=1
X=220
Y=79

;-----CPU Clock-----
[CPUClocktext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=13.5
StringAlign=Left
Antialias=1
Text=CPU Clock
X=220
Y=119

[measureCPUClock]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0000300
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x6000000
HWiNFOType=CurrentValue
MinValue=0
MaxValue=6200
UpdateDivider=33

[CPUClock]
Meter=String
MeasureName=measureCPUClock
StringAlign=Left
NumOfDecimals=0
FontFace=Titillium Web SemiBold
Text=%1
FontColor=000000
FontSize=44
Antialias=1
X=220
Y=186

[CPUClockMHztext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=17
StringAlign=Right
Antialias=1
Text=MHz
X=404
Y=227

;-----CPU Usage-----
[CPUUsagetext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=CPU Usage
X=220
Y=284

[measureCPUUsage]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0000300
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x7000031
HWiNFOType=CurrentValue
MinValue=0
MaxValue=100
UpdateDivider=33

;No time-based
;[measureCPUUsage]
Measure=CPU
;Processor=0

[CPUUtilizationtext]
Meter=String
MeasureName=measureCPUUsage
NumOfDecimals=1
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1 % 
X=220
Y=306
UpdateDivider=33

;[Utilizationtext]
;Meter=String
;FontFace=Titillium Web SemiBold
;FontColor=000000
;FontSize=17
;StringAlign=Right
;Antialias=1
;Text=%
;X=316
;Y=314

;-----CPU Vcore-----
[Vcoretext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=Vcore
X=341
Y=284

[measureCPUVcore]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0087952
HWiNFOSensorInstance=0x1
HWiNFOEntryId=0x2000000
HWiNFOType=CurrentValue
UpdateDivider=33

[CPUVcore]
Meter=String
MeasureName=measureCPUVcore
NumOfDecimals=3
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1 v
X=341
Y=306

;[CPUVcoretext]
;Meter=String
;FontFace=Titillium Web SemiBold
;FontColor=000000
;FontSize=17
;StringAlign=Right
;Antialias=1
;Text=V
;X=341
;Y=315

;-----CPU Power-----
[CPUPowerContext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=CPU Power
X=462
Y=284

[measureCPUPower]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0002a00
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x5000000
HWiNFOType=CurrentValue
UpdateDivider=33

[measureCPUPowerMax]
Measure=Plugin
Plugin=HWiNFO
Group=Sensors
HWiNFOSensorId=0xf0002a00
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x5000000
HWiNFOType=MaximumValue
UpdateDivider=33

[CPUPower]
Meter=String
MeasureName=measureCPUPower
NumOfDecimals=0
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1 w
X=462
Y=306

[CPUPowerMax]
Meter=String
MeasureName=measureCPUPowerMax
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=12
StringAlign=Left
Antialias=1
Text=max: %1 w
X=462
Y=344

;[CPUPowertext]
;Meter=String
;FontFace=Titillium Web SemiBold
;FontColor=000000
;FontSize=17
;StringAlign=Right
;Antialias=1
;Text=W
;X=462
;Y=315

;-----CPU Temp-----
[measureCPUTemp]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0002a00
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x1000004
HWiNFOType=CurrentValue
MinValue=15
MaxValue=115
UpdateDivider=33

[measureCPUTempCalc]
Measure=Calc
Formula=( Clamp ( measureCPUTemp, 0, 99 ))
UpdateDivider=33

[measureCPUTempMax]
Measure=Plugin
Plugin=HWiNFO
Group=Sensors
HWiNFOSensorId=0xf0002a00
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x1000004
HWiNFOType=MaximumValue
UpdateDivider=33

[CPUtemp]
Meter=String
MeasureName=measureCPUTempCalc
FontFace=Titillium Web SemiBold
FontColor=212121
FontSize=30
StringAlign=Left
Antialias=1
Text=%1[\x00B0]C
X=466
Y=154

[CPUtempMaxtext]
Meter=String
MeasureName=measureCPUTempMax
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=12
StringAlign=Left
Antialias=1
Text=max: %1[\x00B0]C
X=470
Y=204


;=====GPU=====

;-----GPU Name-----
[measureGPUName]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOType=SensorName
RegExpSubstitute=1
Substitute="^(.*]: )NVIDIA\s*|AMD\s*|":"","(: .*)$":""
UpdateDivider=-1

[GPUName]
Meter=String
MeasureName=measureGPUName
StringAlign=Left
FontFace=AORUS
;For Auto enable the following line otherwise edit the line above.
Text=%1
FontColor=212121
FontSize=15
Antialias=1
X=646
Y=79

;-----FPS-----
[FPStext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=13.5
StringAlign=Left
Antialias=1
Text=FPS
X=645
Y=119

[measureFPS]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0000ff0
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x8000000
HWiNFOType=CurrentValue
UpdateDivider=33

[measureFPSMax]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0000ff0
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x8000000
HWiNFOType=MaximumValue
UpdateDivider=33

[FPScount]
Meter=String
MeasureName=measureFPS
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=30
StringAlign=Left
Antialias=1
Text=%1 
X=643
Y=154

[FPScountMax]
Meter=String
MeasureName=measureFPSMax
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=12
StringAlign=Left
Antialias=1
Text=max: %1 
X=645
Y=204

;-----GPU Power-----
[GPUPowertext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=13.5
StringAlign=Left
Antialias=1
Text=GPU Power
X=745
Y=119

[measureGPUPower]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOEntryId=0x5000000
HWiNFOType=CurrentValue
UpdateDivider=33

[GPUPower]
Meter=String
MeasureName=measureGPUPower
NumOfDecimals=0
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=30
StringAlign=Left
Antialias=1
Text=%1 w
X=743
Y=154

[measureGPUPowerMax]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOEntryId=0x5000000
HWiNFOType=MaximumValue
UpdateDivider=33

[GPUPowerMax]
Meter=String
MeasureName=measureGPUPowerMax
NumOfDecimals=0
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=12
StringAlign=Left
Antialias=1
Text=max: %1 w
X=743
Y=204

;-----GPU Usage-----
[GPUUsagetext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=GPU Usage
X=644
Y=284

[measureGPUUsage]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOEntryId=0x7000000
HWiNFOType=CurrentValue
MinValue=0
MaxValue=100
UpdateDivider=33

[GPUUsage]
Meter=String
MeasureName=measureGPUUsage
NumOfDecimals=1
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1 %
X=644
Y=306

;[GPUUtilizationMHztext]
;Meter=String
;FontFace=Titillium Web SemiBold
;FontColor=000000
;FontSize=17
;StringAlign=Right
;Antialias=1
;Text=%
;X=741
;Y=315

;-----GPU Clock-----
[GPUClocktext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=GPU Clock
X=764
Y=284

[measureGPUClock]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOEntryId=0x6000000
HWiNFOType=CurrentValue
MinValue=0
MaxValue=3000
UpdateDivider=33

[GPUClock]
Meter=String
MeasureName=measureGPUClock
StringAlign=Left
NumOfDecimals=0
FontFace=Titillium Web SemiBold
Text=%1
FontColor=000000
FontSize=23
Antialias=1
X=761
Y=337

[GPUClockMHztext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=17
StringAlign=Right
Antialias=1
Text=MHz
X=886
Y=346

;-----GPU MEM Clock-----
[GPUMEMClocktext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10
StringAlign=Left
Antialias=1
Text=GPU Mem. Clock
X=896
Y=284

[measureGPUMEMClock]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOEntryId=0x6000004
HWiNFOType=CurrentValue
UpdateDivider=33

[GPUMEMClock]
Meter=String
MeasureName=measureGPUMEMClock
StringAlign=Left
NumOfDecimals=0
FontFace=Titillium Web SemiBold
Text=%1
FontColor=000000
FontSize=23
Antialias=1
X=895
Y=306

[GPUMEMClockMHztext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=17
StringAlign=Right
Antialias=1
Text=MHz
X=1017
Y=315

;-----GPU Temp-----
[measureGPUTemp]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOEntryId=0x1000000
HWiNFOType=CurrentValue
MinValue=15
MaxValue=100
UpdateDivider=33

[measureGPUTempCalc]
Measure=Calc
Formula=( Clamp ( measureGPUTemp, 0, 99 ))
UpdateDivider=33

[measureGPUTempMax]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xe0002000
HWiNFOSensorInstance=#GPUHWiNFOSensorInstance#
HWiNFOEntryId=0x1000000
HWiNFOType=MaximumValue
UpdateDivider=33

[GPUtemp]
Meter=String
MeasureName=measureGPUTempCalc
FontFace=Titillium Web SemiBold
FontColor=212121
FontSize=30
StringAlign=Left
Antialias=1
Text=%1[\x00B0]C
X=888
Y=154

[GPUtempMaxtext]
Meter=String
MeasureName=measureGPUTempMax
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=12
StringAlign=Left
Antialias=1
Text=max: %1[\x00B0]C
X=897
Y=204


;=====System Info.=====

;-----System Info. Title and Icon-----
[SYStext]
Meter=String
FontFace=AORUS
FontColor=212121
FontSize=15
StringAlign=Left
Antialias=1
Text=SYSTEM INFO.
X=220
Y=441

[SYSIcon]
Meter=Image
ImageName=#@#\Images\Icons\info.png
X=385
Y=438
AntiAlias=1


;-----Memory Usage-----
[MEMUsagetext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=Mem. Usage
X=220
Y=480

[measureMEMUsage]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0000301
HWiNFOSensorInstance=0x0
HWiNFOEntryId=0x8000005
HWiNFOType=CurrentValue
MinValue=0
MaxValue=100
UpdateDivider=33

[MEMUtilization]
Meter=String
MeasureName=measureMEMUsage
NumOfDecimals=1
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1 %
X=220
Y=502

;[MEMUsagetext]
;Meter=String
;FontFace=Titillium Web SemiBold
;FontColor=000000
;FontSize=17
;StringAlign=Right
;Antialias=1
;Text=% 
;X=316
;Y=511

;-----MEM Clock-----
[MEMSpeedtext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=Memory Clock
X=341
Y=480

[measureMEMClock]
Measure=Plugin
Plugin=HWiNFO
HWiNFOSensorId=0xf0000301
HWiNFOSensorInstance=0x1
HWiNFOEntryId=0x6000000
HWiNFOType=CurrentValue
UpdateDivider=300

[measureMEMClock2x]
Measure=Calc
Formula=( 2 * measureMEMClock )
UpdateDivider=300

[MEMClocktext]
Meter=String
MeasureName=measureMEMClock2x
NumOfDecimals=0
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1
X=341
Y=502

[MEMClockMHztext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=17
StringAlign=Right
Antialias=1
Text=MHz
X=476
Y=511

;-----UpTime-----
[Uptimetext]
Meter=String
FontFace=Titillium Web SemiBold
FontColor=6C6C6C
FontSize=10.5
StringAlign=Left
Antialias=1
Text=System Uptime
X=501
Y=480

[MeasureUptime]
Measure=Uptime
Format=%3!02i!:%2!02i!

[MeterUptime]
Meter=String
MeasureName=MeasureUptime
FontFace=Titillium Web SemiBold
FontSize=23
FontColor=000000
AntiAlias=1
Text=%1
X=501
Y=502


;=====Network=====

;-----Network Title and Icon-----
[NETtext]
Meter=String
FontFace=AORUS
FontColor=212121
FontSize=15
StringAlign=Left
Antialias=1
Text=NETWORK
X=646
Y=441

[NETIcon]
Meter=Image
ImageName=#@#\Images\Icons\net.png
X=765
Y=438
AntiAlias=1

[Variables]
MaxDownload=10485760
MaxUpload=10485760

;-----DLrate-----
[measureDownload]
Measure=NetIn
NetInSpeed=#MaxDownload#
UpdateDivider=33

[CurrentDLrate]
Meter=String
MeasureName=measureDownload
NumOfDecimals=1
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1B/s
Autoscale=1
X=675
Y=525

[ImageMeterDLrate]
Meter=Image
ImageName=#@#\Images\Icons\DLrate.png
X=646
Y=548
AntiAlias=1

;-----ULrate-----
[measureUpload]
Measure=NetOut
NetOutSpeed=#MaxUpload#
UpdateDivider=33

[CurrentULrate]
Meter=String
MeasureName=measureUpload
NumOfDecimals=1
FontFace=Titillium Web SemiBold
FontColor=000000
FontSize=23
StringAlign=Left
Antialias=1
Text=%1B/s
AutoScale=1
X=676
Y=480

[ImageMeterULrate]
Meter=Image
ImageName=#@#\Images\Icons\ULrate.png
X=646
Y=500
AntiAlias=1


;=====Socials=====

;-----FB-----
[FBIcon]
Meter=Image
ImageName=#@#\Images\Icons\FB.png
X=936
Y=565
AntiAlias=1
LeftMouseUpAction=["https://www.facebook.com/AorusOfficial/"]

;-----IG-----
[IGIcon]
Meter=Image
ImageName=#@#\Images\Icons\IG.png
X=966
Y=566
H=17
AntiAlias=1
LeftMouseUpAction=["https://www.instagram.com/aorus_official/"]

;-----X-----
[XIcon]
Meter=Image
ImageName=#@#\Images\Icons\X.png
X=994
Y=566
H=15
AntiAlias=1
LeftMouseUpAction=["https://www.x.com/aorusofficial"]