`sample.mp4` is an original synthetic one-second video test pattern, generated with:

```
ffmpeg -f lavfi -i testsrc2=size=320x180:rate=12 -t 1 -c:v libx264 -pix_fmt yuv420p -movflags +faststart -an sample.mp4
```

No footage, external asset or FFmpeg executable is distributed with ClipDrop.
The fixture is for native thumbnail/preview checks only.

`ImageStackProbe` embeds an original synthetic 3 × 2 lossless WebP, generated
with Pillow from opaque red, transparent and half-transparent pixels. It supplies
independent alpha/size/encoded-byte checks without downloading external images.
