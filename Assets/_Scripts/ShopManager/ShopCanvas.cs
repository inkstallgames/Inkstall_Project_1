using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopCanvas : MonoBehaviour
{
  private void OnEnable()
  {
    CoinsManager.Instance.UpdateCoinUI();
  }
}
