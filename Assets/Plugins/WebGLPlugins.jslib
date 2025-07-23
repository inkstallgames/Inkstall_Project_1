mergeInto(LibraryManager.library, {

  GetStudentId: function() {
    var studentId = localStorage.getItem('studentId');
    if (studentId === null) {
        console.error('studentId not found in localStorage.');
        return "";
    }
    var buffer = _malloc(studentId.length + 1);
    stringToUTF8(studentId, buffer, studentId.length + 1);
    return buffer;
  },

});
