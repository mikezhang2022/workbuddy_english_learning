window.factoryReportPwa = {
  _dotNetRef: null,
  getOnlineStatus: function () {
    return navigator.onLine;
  },
  registerOnlineHandlers: function (dotNetRef) {
    this._dotNetRef = dotNetRef;
    this._onlineHandler = () => dotNetRef.invokeMethodAsync('OnOnlineChanged', true);
    this._offlineHandler = () => dotNetRef.invokeMethodAsync('OnOnlineChanged', false);
    window.addEventListener('online', this._onlineHandler);
    window.addEventListener('offline', this._offlineHandler);
  },
  unregisterOnlineHandlers: function () {
    if (this._onlineHandler) {
      window.removeEventListener('online', this._onlineHandler);
    }
    if (this._offlineHandler) {
      window.removeEventListener('offline', this._offlineHandler);
    }
    this._dotNetRef = null;
  }
};
