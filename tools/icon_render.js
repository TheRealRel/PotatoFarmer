const {chromium}=require('playwright'); const fs=require('fs');
(async()=>{
 const b=await chromium.launch({args:['--use-gl=swiftshader','--enable-webgl','--ignore-gpu-blocklist']}); const p=await b.newPage();
 p.on('console',m=>console.log('page:',m.text())); p.on('pageerror',e=>console.log('err',e.message));
 await p.goto('http://127.0.0.1:8765/page.html'); await p.waitForFunction(()=>window.ready);
 const jobs=JSON.parse(fs.readFileSync(process.argv[2]));
 for(const j of jobs){const d=await p.evaluate(([n,y,pi,f])=>window.render(n,y,pi,f),[j.names,j.yaw,j.pitch,j.flip||false]); fs.writeFileSync(j.out,Buffer.from(d.split(',')[1],'base64'));}
 await b.close();})();
