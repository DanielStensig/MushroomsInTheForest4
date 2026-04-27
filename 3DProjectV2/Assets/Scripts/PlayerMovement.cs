using Unity.Netcode;
using UnityEngine;
public class PlayerMovement : NetworkBehaviour
{
    public float speed = 5f;
    void Update()
    {
        if (!IsOwner) return;
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move =
        new Vector3(h, 0f, v) * speed * Time.deltaTime;
        transform.Translate(move, Space.World);
    }
}
