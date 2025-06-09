using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySphereCoolYeah : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame  
    void Update()
    {
        float scale = Mathf.Abs(Mathf.Sin(Time.time)) + 0.5f;
        transform.localScale = new Vector3(scale, scale, transform.localScale.z);
    }
}
