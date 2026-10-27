`sample.mp4` is an original synthetic one-second video test pattern, generated with:

```
ffmpeg -f lavfi -i testsrc2=size=320x180:rate=12 -t 1 -c:v libx264 -pix_fmt yuv420p -movflags +faststart -an sample.mp4
```

No footage, external asset or FFmpeg executable is distributed with ClipDrop.
The fixture is for native thumbnail/preview checks only.
