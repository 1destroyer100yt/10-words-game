mergeInto(LibraryManager.library, {
  QuitDebtGame: function () {
    setTimeout(function () {
      if (typeof window.quitDebtGame === 'function') window.quitDebtGame();
    }, 0);
  }
});
