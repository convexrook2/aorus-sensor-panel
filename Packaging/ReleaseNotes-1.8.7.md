# v1.8.7 — Native ship image sizing

The native monitor now sizes its WebView capture for the surrounding display
scale. Enlarging the monitor previously stretched a smaller captured ship image,
softening the left showcase and holotable. The browser surface now renders enough
pixels for its actual displayed size, while retaining the existing layout.

The latest models, maps, lighting, 4x MSAA, shadows, ambient occlusion, rotation
speed and frame/capture timing remain unchanged. Resize and DPI changes update
the capture dimensions; there is no new polling or rendering loop. Fleet Studio
is separate and unchanged.

Visible VS Code native saved-data checks covered portrait and holotable views at
1024x576 and 1920x1080 window sizes. At the larger size, the model canvas changed
from 236x544 to 342x787 in the left panel and from 1264x522 to 1828x757 in the
holotable, matching their measured physical display sizes. Those checks establish
native sizing, not a new live hardware/performance result or installed acceptance.
