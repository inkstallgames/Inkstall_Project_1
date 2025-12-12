using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoAds : MonoBehaviour
{
   public GameObject noAdsPanel;

    public void OnclickNoAdsBtn()
    {
        noAdsPanel.SetActive(true);

    }

    public void OnclickClosebtn()
    {
        noAdsPanel.SetActive(false);
    }
}
