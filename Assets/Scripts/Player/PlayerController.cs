using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {
   //protected FixedJoystick joystick;

   private Rigidbody rb;
   private Transform transform;
   private float lastAngle = 90f;
   public float movementSpeed = 5f;
   private CharacterController cc;

   // Start is called before the first frame update
   void Start() {
      //joystick = FindObjectOfType<FixedJoystick>();

      //rb = GetComponent<Rigidbody>();
      //rb.useGravity = false;
      transform = GetComponent<Transform>();
      cc = GetComponent<CharacterController>();
   }

   // Update is called once per frame
   void Update() {
      float rotX = Input.GetAxis("Mouse X");
      transform.Rotate(0, rotX, 0);

      float forwardSpeed = Input.GetAxis("Vertical") * movementSpeed;
      float sideSpeed = Input.GetAxis("Horizontal") * movementSpeed;
      float vertSpeed = Input.GetAxis("Jump") * movementSpeed;

      Vector3 speed = new Vector3(sideSpeed, vertSpeed, forwardSpeed);

      cc.SimpleMove(speed);
   }
}
