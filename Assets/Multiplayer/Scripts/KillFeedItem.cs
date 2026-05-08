using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class KillFeedItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI killerText;
    [SerializeField] private TextMeshProUGUI victimText;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private Image backgroundImage; // Single background image instead of two
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;

    [Header("Colors (Team Based)")]
    [SerializeField] private Color heroColor = new Color(0.3f, 0.6f, 1f); // Blue
    [SerializeField] private Color alienColor = new Color(0.9f, 0.2f, 0.2f); // Red
    [SerializeField] private Color defaultTextColor = new Color(1f, 1f, 1f); // White
    
    [Header("Animation Settings")]
    [SerializeField] private float slideInDuration = 0.2f;
    [SerializeField] private float displayDuration = 4f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    public void Setup(string killerName, string victimName, Sprite weaponSprite, int killerTeam, int victimTeam)
    {
        if (killerText != null)
        {
            killerText.text = killerName;
            killerText.color = GetTeamColor(killerTeam);
        }

        if (victimText != null)
        {
            victimText.text = victimName;
            victimText.color = GetTeamColor(victimTeam);
        }
        
        if (weaponIcon != null)
        {
            if (weaponSprite != null)
            {
                weaponIcon.sprite = weaponSprite;
                weaponIcon.gameObject.SetActive(true);
            }
            else
            {
                weaponIcon.gameObject.SetActive(false); // Hide if no sprite provided
            }
        }

        StartCoroutine(AnimateKillFeed());
    }

    private Color GetTeamColor(int teamId)
    {
        if (teamId == 0) return heroColor;
        if (teamId == 1) return alienColor;
        return defaultTextColor;
    }

    private IEnumerator AnimateKillFeed()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        // Wait a frame for Layout Group to position it correctly
        yield return new WaitForEndOfFrame();

        // Fade In and slightly Scale up (avoiding anchoredPosition conflicts with Layout Group)
        float time = 0;
        
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        rectTransform.localScale = new Vector3(0.8f, 0.8f, 1f);

        while (time < slideInDuration)
        {
            time += Time.deltaTime;
            float t = time / slideInDuration;
            float smoothT = t * t * (3f - 2f * t); 
            
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(0f, 1f, smoothT);
            rectTransform.localScale = Vector3.Lerp(new Vector3(0.8f, 0.8f, 1f), Vector3.one, smoothT);
            
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        rectTransform.localScale = Vector3.one;

        // Wait
        yield return new WaitForSeconds(displayDuration);

        // Fade Out
        time = 0;
        while (time < fadeOutDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeOutDuration;
            
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        // Destroy after animation
        Destroy(gameObject);
    }
}
