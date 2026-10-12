;=====Graphs, Bars and Rings=====


;-----CPU-----

;-----CPU Graph-----
[BackgroundCPUgraph]
Meter=Image
X=220
Y=152
W=183
H=55
SolidColor=434343

[LineGraphCPU]
Meter=Line
MeasureName=measureCPUClock
GraphStart=Right
LineColor=FF6400
UpdateDivider=35
HorizontalLineColor=FF6400
AntiAlias=1
LineWidth=1.5
AutoScale=1
X=220
Y=152
W=183
H=54

;-----CPU Usage Bar-----
[CPUUtilizationBarBack]
Meter=Image
X=220
Y=352
W=109
H=9
ImageName=#@#Images\Bars\empty.png
UpdateDivider=25

[CPUUtilizationBar]
Meter=Bar
MeasureName=measureCPUUsage
BarOrientation=Horizontal
X=220
Y=352
W=109
H=9
Flip=0
BarImage=#@#Images\Bars\full.png
UpdateDivider=25

;-----CPU Ring-----
[ImageMeterCPU]
Meter=Image
ImageName=#@#\Images\Rings\Ring.png
X=440
Y=121
AntiAlias=1

[CPUtempcircle]
Meter=Roundline
MeasureName=measureCPUTemp
X=508
Y=189
StartAngle=(Rad(270))
RotationAngle=(Rad(360))
LineStart=55
LineLength=54
LineColor=FF6400
Solid=1
AntiAlias=1


;-----GPU-----

;-----GPU Ring-----
[ImageMeterGPU]
Meter=Image
ImageName=#@#\Images\Rings\Ring.png
X=864
Y=121
AntiAlias=1

[GPUtempring]
Meter=Roundline
MeasureName=measureGPUTemp
X=932
Y=189
StartAngle=(Rad(270))
RotationAngle=(Rad(360))
LineStart=55
LineLength=54
LineColor=FF6400
Solid=1
AntiAlias=1

;-----GPU Usage Bar-----
[GPUUsageBarBack]
Meter=Image
X=644
Y=352
W=109
H=9
ImageName=#@#\Images\Bars\empty.png
UpdateDivider=33

[GPUUsageBar]
Meter=Bar
MeasureName=measureGPUUsage
BarOrientation=Horizontal
X=644
Y=352
W=109
H=9
Flip=0
BarImage=#@#\Images\Bars\full.png
UpdateDivider=33

;-----GPU Graph-----
[BackgroundGPUgraph]
Meter=Image
X=764
Y=309
W=118
H=35
SolidColor=434343

[LineGraphGPU]
Meter=Line
MeasureName=measureGPUClock
LineColor=FF6400
LineCount=1
UpdateDivider=35
AntiAlias=1
AustoScale=1
LineWidth=1.5
X=764
Y=309
W=118
H=34


;=====System Info=====

;-----MEM Usage Bar-----
[MEMUsageBarBack]
Meter=Image
X=220
Y=548
W=109
H=9
ImageName=#@#\Images\Bars\empty.png
UpdateDivider=33

[MEMUsageBar]
Meter=Bar
MeasureName=measureMEMUsage
BarOrientation=Horizontal
X=220
Y=548
W=109
H=9
Flip=0
BarImage=#@#\Images\Bars\full.png
UpdateDivider=33