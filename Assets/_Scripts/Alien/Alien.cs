using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Alien : MonoBehaviour
{
    // Start is called before the first frame update
    void OnEnable()
    {
        StartCoroutine(DestroyAlien());
    }

    public IEnumerator DestroyAlien()
    {
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }

    
}
 