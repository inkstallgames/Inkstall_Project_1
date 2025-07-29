mergeInto(LibraryManager.library, 
{
  GetUserIdFromLocalStorage: function() 
  {
    var userId = localStorage.getItem('userId');
    
    // Send the found userId to the UserIDManager script in unity
    if(userId)
    {
      // SendMessage('GameObjectName', 'MethodName', 'parameter');
      SendMessage("UserIDManager", "ReceiveUserId", userId)
    }
    else 
    {
        console.error('userId not found in localStorage.');
    }
  },
});
