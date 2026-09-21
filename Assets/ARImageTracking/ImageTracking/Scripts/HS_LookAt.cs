using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// This complete script can be attached to a camera to make it
// continuously point at another object.

public class HS_LookAt : MonoBehaviour
{
    [SerializeField]
    private Camera target;

    private void Awake()
    {
        target = Camera.main; //FindObjectOfType<Camera>();
    }
    private void Start()
    { 
          
           if(transform.GetChild(0))
            {
               if( transform.GetChild(0).GetChild(0))
                {
                    transform.GetChild(0).GetChild(0).GetComponent<RectTransform>().Rotate(new Vector3(0, 180, 0), Space.Self);
                }
            }
    }
    void Update()
    {
        // Rotate the camera every frame so it keeps looking at the target
        
        if(target != null)
        {
            this.gameObject.transform.LookAt(target.gameObject.transform);
        }
    }
}