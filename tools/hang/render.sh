#!/bin/bash
# render.sh desc.json out.png : serves this folder, renders, stops the server
cd /root/rackview
python3 -m http.server 8765 >/dev/null 2>&1 & S=$!
sleep 1
timeout 500 node scene.js "$1" "$2"; R=$?
kill $S
exit $R
