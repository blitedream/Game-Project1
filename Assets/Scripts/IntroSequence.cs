using System.Collections;
using TMPro;
using UnityEngine;

public class IntroSequence : MonoBehaviour
{
    public GameObject introPanel;
    public TextMeshProUGUI introText;
    public MonoBehaviour playerController;

    public float lineDuration = 3.4f;

    [TextArea(2, 4)]
    public string[] lines =
    {
        "Before the Zacaton assignment,\nyou are brought back to the training room.",
        "A diver does not die only from depth.\nPanic, darkness, and bad habits do the rest.",
        "The rescue team has one rule:\nif you cannot stay calm here, you do not enter open water.",
        "Today you will repeat the basics:\ngear, breathing, entry, descent, return.",
        "Controls:\nWASD move, mouse look, E interact.",
        "Underwater:\nHold C to descend, hold Space to ascend.",
        "The pool current changes direction without warning.\nUse small corrections instead of fighting it.",
        "The pool is controlled.\nThe cave will not be.",
        "Wear the equipment.\nEnter the water when you are ready."
    };

    private void Start()
    {
        StartCoroutine(PlayIntro());
    }

    private IEnumerator PlayIntro()
    {
        if (playerController != null)
            playerController.enabled = false;

        if (introPanel != null)
            introPanel.SetActive(true);

        foreach (string line in lines)
        {
            if (introText != null)
                introText.text = line;

            yield return new WaitForSeconds(lineDuration);
        }

        if (introPanel != null)
            introPanel.SetActive(false);

        if (playerController != null)
            playerController.enabled = true;
    }
}
