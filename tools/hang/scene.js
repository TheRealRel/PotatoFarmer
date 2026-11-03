// node scene.js desc.json out.png
const {chromium}=require('playwright');const fs=require('fs');
(async()=>{const desc=JSON.parse(fs.readFileSync(process.argv[2]));const cols=desc.cols||1,W=desc.w||desc.tile||800,H=desc.h||desc.tile||800,rows=Math.ceil(desc.tiles.length/cols);
const b=await chromium.launch({args:['--use-gl=swiftshader','--enable-webgl','--ignore-gpu-blocklist']});const p=await b.newPage({viewport:{width:cols*W,height:rows*H}});
p.on('pageerror',e=>console.log('err',e.message));p.on('console',m=>{if(!m.text().includes('GPU stall'))console.log('page',m.text())});
await p.goto('http://localhost:8765/scene.html');await p.waitForFunction('window.ready===true');
await p.evaluate(d=>window.load(d),desc);await p.screenshot({path:process.argv[3]});await b.close();})();
