using UnityEngine;

// Jednoduché otáčení objektu kolem osy (např. kotoučová pila na překážce).
public class Spin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;   // kolem které osy (v souřadnicích objektu)
    public float speed = 360f;          // stupně za sekundu

    void Update() => transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
}
