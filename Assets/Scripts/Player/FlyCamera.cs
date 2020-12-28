using UnityEngine;
using System.Collections;

public class FlyCamera : MonoBehaviour {
   public Camera camera;
   float mainSpeed = 100f; //regular speed
   float camSens = 0.4f; //How sensitive it with mouse
   private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
   private float totalRun = 1.0f;

   void Update() {
      lastMouse = Input.mousePosition - lastMouse;
      lastMouse = new Vector3(-lastMouse.y * camSens, lastMouse.x * camSens, 0);
      Vector3 camRot = new Vector3(camera.transform.eulerAngles.x + lastMouse.x, camera.transform.eulerAngles.y + lastMouse.y, 0);
      Vector3 bodyRot = new Vector3(0, transform.eulerAngles.y + lastMouse.y, 0);
      transform.eulerAngles = bodyRot;
      camera.transform.eulerAngles = camRot;
      lastMouse = Input.mousePosition;
      //Mouse  camera angle done.  

      //Keyboard commands
      float f = 0.0f;
      Vector3 p = GetBaseInput();

      totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
      p *= mainSpeed * Time.deltaTime;
      Vector3 newPosition = transform.position;
      transform.Translate(p);
   }

   private Vector3 GetBaseInput() { //returns the basic values, if it's 0 than it's not active.
      Vector3 p_Velocity = new Vector3();
      if (Input.GetKey(KeyCode.W)) {
         p_Velocity += new Vector3(0, 0, 1);
      }
      if (Input.GetKey(KeyCode.S)) {
         p_Velocity += new Vector3(0, 0, -1);
      }
      if (Input.GetKey(KeyCode.A)) {
         p_Velocity += new Vector3(-1, 0, 0);
      }
      if (Input.GetKey(KeyCode.D)) {
         p_Velocity += new Vector3(1, 0, 0);
      }
      if (Input.GetKey(KeyCode.Space)) {
         p_Velocity += new Vector3(0, 1, 0);
      }
      if (Input.GetKey(KeyCode.LeftShift)) {
         p_Velocity += new Vector3(0, -1, 0);
      }
      return p_Velocity;
   }
}