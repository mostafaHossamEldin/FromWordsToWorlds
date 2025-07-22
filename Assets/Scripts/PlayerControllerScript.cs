using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerScript : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public Transform head;
    public Camera cam;

    [Header("Configurations")]
    public float walkSpeed;
    public float runSpeed;
    public float jumpSpeed;

    [Header("RunTime")]
    Vector3 newVelocity;
    bool isGrounded = false;

    [Header("smthElse")]
    public bool isMoving;
    public ArrayList nearbyBots = new ArrayList();

    // Start is called before the first frame update
    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        isMoving = true;
    }

    // Update is called once per frame
    void Update()
    {
        if(nearbyBots.Count > 0)
            isMoving = false;
        else
            isMoving = true;

        if (isMoving)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * 2f);

            newVelocity = Vector3.up * rb.velocity.y;
            float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            newVelocity.x = Input.GetAxis("Horizontal") * speed;
            newVelocity.z = Input.GetAxis("Vertical") * speed;

            if (isGrounded && Input.GetKey(KeyCode.Space))
            {
                newVelocity.y = jumpSpeed;
            }
        }
    }

    void FixedUpdate()
    {
        if (isMoving)
            rb.velocity = transform.TransformDirection(newVelocity);
    }

    void LateUpdate()
    {
        if (isMoving)
        {
            Vector3 e = head.eulerAngles;
            e.x -= Input.GetAxis("Mouse Y") * 2f;
            e.x = RestrictAngle(e.x, -85, 85);
            head.eulerAngles = e;
        }
    }

    public static float RestrictAngle(float angle, float angleMin, float angleMax)
    {
        if (angle > 180)
            angle -= 360;
        else if (angle < -180)
            angle += 360;

        if (angle > angleMax)
            angle = angleMax;
        if (angle < angleMin)
            angle = angleMin;

        return angle;
    }

    void OnCollisionStay(Collision collision)
    {
        isGrounded = true;
        // isJumping = false;
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
