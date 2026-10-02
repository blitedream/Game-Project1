using UnityEngine;

public class PlayerGearState : MonoBehaviour
{
    public bool hasDivingGear = false;

    private void Start()
    {
        if (GetComponent<PlayerBreathingAudio>() == null)
            gameObject.AddComponent<PlayerBreathingAudio>();
    }

    public void EquipDivingGear()
    {
        hasDivingGear = true;
        Debug.Log("Player entered equipment mode.");
    }
}
