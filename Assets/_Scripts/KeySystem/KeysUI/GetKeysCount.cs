using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GetKeysCount : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI keyCountText;
    private int currentKeys;
    
    void Start()
    {
        currentKeys = KeyManager.Instance.GetKeyCount();
        UpdateKeyCountUI();
    }

    private void UpdateKeyCountUI()
    {
        if (keyCountText != null)   
        {
            keyCountText.text = $"Keys: {currentKeys}";
        }
    }
}
