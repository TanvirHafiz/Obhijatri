// Obhijatri page bridge. Injected into every frame before page scripts run, wrapped in a function
// so nothing here is visible to the page. Two secrets are used:
//  - outToken is sent with every message to the app. It never leaves this closure in any other
//    way, so page scripts cannot learn it and cannot forge commands to the app.
//  - inPrefix marks messages from the app. Page scripts can read those, so it only protects
//    against accidental matches; nothing sensitive is ever sent to the page.
"use strict";
const hostApi = window.chrome && window.chrome.webview;
if (!hostApi) { return; }
const post = hostApi.postMessage.bind(hostApi);
const listenHost = hostApi.addEventListener.bind(hostApi);
const outToken = "__OUT_TOKEN__";
const inPrefix = "__IN_TOKEN__:";
const send = (command, payload) => {
  post(outToken + ":" + command + (payload === undefined ? "" : ":" + payload));
};
const hostHandlers = Object.create(null);
listenHost("message", (e) => {
  const data = e.data;
  if (typeof data !== "string" || data.slice(0, inPrefix.length) !== inPrefix) { return; }
  const rest = data.slice(inPrefix.length);
  const colon = rest.indexOf(":");
  const command = colon < 0 ? rest : rest.slice(0, colon);
  const handler = hostHandlers[command];
  if (handler) { handler(colon < 0 ? "" : rest.slice(colon + 1)); }
});
