using System.Collections;
using UnityEngine;

public class WasteContainer : MonoBehaviour
{
    public GameObject openContainer;
    public GameObject closeContainer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        closeContainer.SetActive(false);
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Forceps"))
        {
            closeContainer.SetActive(true);
            openContainer.SetActive(false);
        }
    }
    
}
