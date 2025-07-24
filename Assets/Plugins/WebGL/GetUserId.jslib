mergeInto(LibraryManager.library, 
{
  GetUserIdFromLocalStorage: function() 
  {
    var userId = localStorage.getItem('userId');
    if(userId)
    {
      // Send the found userId to the UserIDBridge script in unity
      // SendMessage('GameObjectName', 'MethodName', 'parameter');
      SendMessage("GameManager", "ReceiveUserId", userId)
    }
    else 
    {
        console.error('user Id not found in localStorage.');
    }
  },  
});
