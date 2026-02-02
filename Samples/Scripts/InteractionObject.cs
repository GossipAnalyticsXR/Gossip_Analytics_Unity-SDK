using UnityEngine;

public class InteractionObject : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            PlayerDemo o = other.gameObject.GetComponent<PlayerDemo>();
            if (o.currentInteraction == null) o.currentInteraction = this.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerDemo o = other.GetComponent<PlayerDemo>();
            if (o.currentInteraction != null && o.currentInteraction == this.gameObject) o.currentInteraction = null;
        }
    }
}
