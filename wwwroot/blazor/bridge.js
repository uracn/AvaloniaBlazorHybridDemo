// Native <-> Blazor IPC bridge for Avalonia 12's NativeWebView.
// Loaded before blazor.webview.js, which calls window.external.sendMessage /
// window.external.receiveMessage.
(function () {
  var listeners = [];

  // .NET -> JS: AppWebViewManager.SendMessage calls InvokeScript with this entry point.
  window.__blazorReceive = function (message) {
    for (var i = 0; i < listeners.length; i++) listeners[i](message);
  };

  var bridge = {
    sendMessage: function (message) {
      // Avalonia injects invokeCSharpAction into the document; fall back to the
      // raw WebView2 host object in case the injection has not happened yet.
      if (typeof window.invokeCSharpAction === 'function') {
        window.invokeCSharpAction(message);
      } else if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);
      }
    },
    receiveMessage: function (callback) {
      if (typeof callback === 'function') listeners.push(callback);
    }
  };

  try {
    Object.defineProperty(window, 'external', {
      value: bridge, writable: true, configurable: true
    });
  } catch (e) {
    window.external = bridge;
  }

  // The host document is /index.html, but Blazor's Router expects the root path.
  // Normalizing the URL before blazor.webview.js sends AttachPage keeps
  // document.baseURI and location.href consistent.
  if (window.location.pathname !== '/') {
    window.history.replaceState(null, '', '/');
  }
})();
