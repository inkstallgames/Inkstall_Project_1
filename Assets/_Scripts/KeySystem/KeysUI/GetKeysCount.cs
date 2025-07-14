using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GetKeysCount : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI keyCountText;
    private int currentKeys;
    // Start is called before the first frame update
    void Start()
    {
        currentKeys = KeyManager.Instance.GetKeyCount();
        UpdateKeyCountUI();
    }

    // Update the key count UI
    private void UpdateKeyCountUI()
    {
        if (keyCountText != null)   
        {
            keyCountText.text = $"Keys: {currentKeys}";
        }
    }
}
