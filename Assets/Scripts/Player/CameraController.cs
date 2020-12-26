using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {

   public GameObject player;
   public float smoothSpeed = 0.125f;
   public Vector3 offset;
   private Transform transform;
   private Transform playerTransform;

   //protected FixedJoystick joystick;

   // Start is called before the first frame update
   void Start() {
      transform = GetComponent<Transform>();
      playerTransform = player.GetComponent<Transform>();

      //joystick = FindObjectOfType<FixedJoystick>();
   }

   // Update is called once per frame
   void FixedUpdate() {
      /*Vector3 joystickDir = new Vector3(joystick.Horizontal * 5f, 0, joystick.Vertical * 5f);

      Vector3 desiredPosition = playerTransform.position + offset + 0.3f * joystickDir;
      Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
      transform.position = smoothedPosition;
      */
   }
}
