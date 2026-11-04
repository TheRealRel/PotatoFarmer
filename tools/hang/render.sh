#!/bin/bash
# render.sh desc.json out.png : serves this folder, renders, stops the server
cd /root/rackview
python3 -m http.server 8765 >/dev/null 2>&1 & S=$!
sleep 1
python3 /root/rackview/fixatt.py "$1" /tmp/scene_fixed.json && timeout 500 node scene.js /tmp/scene_fixed.json "$2"; R=$?
kill $S
exit $R
