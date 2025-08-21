mergeInto(LibraryManager.library, 
{
  GetUserIdFromLocalStorage: function() 
  {
    var userId = localStorage.getItem('studentId');
    
    // Send the found userId to the GetUserIDBridge script in unity
    if(userId)
    {
      // SendMessage('GameObjectName', 'MethodName', 'parameter');
      SendMessage("GetUserIDBridge", "ReceiveUserId", userId);
    }
    else 
    {
        console.log('userId not found in localStorage, using default ID.');
        // Send a signal to use default ID
        SendMessage("GetUserIDBridge", "UseDefaultUserId", "");
    }
  },
});
