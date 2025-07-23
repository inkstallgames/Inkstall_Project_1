mergeInto(LibraryManager.library, {

  GetUserId: function() {
    var userId = localStorage.getItem('userId');
    if (userId === null) {
        console.error('user Id not found in localStorage.');
        return "";
    }
    var buffer = _malloc(userId.length + 1);
    stringToUTF8(userId, buffer, userId.length + 1);
    return buffer;
  },

});
