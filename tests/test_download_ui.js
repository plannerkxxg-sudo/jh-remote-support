'use strict';
// Browser-independent DOM contract: direct same-origin download, no Blob rewriting.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

async function scenario(response, value = '000007') {
  const listeners = {};
  const ids = {};
  for (const id of ['connect-form','support-code','download','feedback','after-download','file-info','file-name','file-hash']) {
    ids[id] = {value, hidden: true, textContent: '', disabled: false,
      firstElementChild: {textContent:''}, addEventListener(name, fn) { listeners[id + ':' + name] = fn; },
      setAttribute(){}, removeAttribute(){}, focus(){}, scrollIntoView(){}};
  }
  const requests = [], downloads = [];
  const sandbox = {
    document: {getElementById: id => ids[id], body:{appendChild(){}}, createElement(tag){
      assert.equal(tag,'a');
      return {click(){downloads.push({href:this.href,download:this.download});},remove(){}};
    }},
    fetch: async (url, options) => { requests.push({url,options}); return response; },
    AbortController, setTimeout, clearTimeout,
  };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../relay/web/app.js'),'utf8'), sandbox);
  await listeners['connect-form:submit']({preventDefault(){}});
  return {ids, requests, downloads};
}

async function run() {
  const info = {version:'0.10', filename:'JHSupport-000007-tls-alice.onrender.com.exe',
    sha256:'a'.repeat(64), size:2048, downloadUrl:'/download?code=000007'};
  const ok = value => ({ok:true,headers:{get:()=> 'application/json'},json:async()=>value});
  const result = await scenario(ok(info));
  assert.deepEqual(result.downloads,[{href:info.downloadUrl,download:info.filename}]);
  assert.equal(result.requests.length,1);
  assert.equal(result.requests[0].url,'/api/download-info?code=000007');
  assert.equal(result.ids['file-hash'].textContent,info.sha256);
  assert.equal(result.ids['after-download'].hidden,false);
  for (const bad of [
    {...info,downloadUrl:'https://other.invalid/tool.exe'}, {...info,filename:'tool.exe.bat'},
    {...info,sha256:'not-a-hash'}, {...info,version:'0.7'}, {...info,size:5},
    {...info,filename:'JHSupport-123456-tls-alice.onrender.com.exe'},
  ]) assert.equal((await scenario(ok(bad))).downloads.length,0);
  const rejected = await scenario({ok:false,json:async()=>({message:'코드가 만료되었습니다.'})});
  assert.equal(rejected.downloads.length,0);
  assert.equal(rejected.ids.feedback.textContent,'코드가 만료되었습니다.');
  assert.equal(rejected.ids.download.disabled,false);
  const invalid = await scenario(ok(info),'12A456');
  assert.equal(invalid.requests.length,0);
  console.log('Download UI: direct attachment, six-digit code, metadata and error checks passed.');
}
run().catch(error=>{console.error(error);process.exitCode=1;});
