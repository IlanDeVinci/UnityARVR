using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class LightHandler: MonoBehaviour
{
    [SerializeField] private Transform thisTransform;
    [SerializeField] private GameObject thisGameObject;
    [SerializeField] private InputAction action;
    [SerializeField] private List<GameObject> sphereObj;
    private bool isLightOn = false; 
    [SerializeField] [Range(0.0f, 200.0f)] private float rotationSpeed;

    private void Start()
    {
        print("Hello World");
        Debug.Log("Hello World");
        Debug.LogWarning("Hello World");
        Debug.LogError("Hello World");

        //thisTransform = GetComponent<Transform>();
        Instantiate(thisGameObject, thisTransform.position + new Vector3(0,0,1), Quaternion.identity);
    }

    // Update is called once per frame
    private void Update()
    {
        if (action.triggered)
        {
            isLightOn = !isLightOn;
            foreach (GameObject obj in sphereObj)
            {
                obj.SetActive(isLightOn);
            }
            thisTransform.Rotate(rotationSpeed * Time.fixedDeltaTime, 0f, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    public void OnEnable()
    {
        action.Enable();
    }

    public void OnDisable()
    {
        action.Disable();
    }

}
