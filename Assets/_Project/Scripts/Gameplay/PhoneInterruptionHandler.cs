using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class PhoneInterruptionHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PuzzleEventDirector director;
    [SerializeField] private MeshRenderer screenRenderer;

    [Header("Scenario 1: Material Cycling")]
    [SerializeField] private string scenario1Prompt = "Press S to scroll";
    [SerializeField] private List<Material> screenMaterials = new List<Material>();
    
    [Header("Scenario 2: Typing")]
    [SerializeField] private string scenario2Prompt = "Type: ";
    [SerializeField] private Material scenario2ScreenMat;
    [SerializeField] private TMP_Text worldTextDisplay; 
    [SerializeField] private string targetMessage = "no clue"; 
    
    [Header("Scenario 3: Like then Scroll")]
    [SerializeField] private string scenario3LikePrompt = "Press Q to like";
    [SerializeField] private string scenario3ScrollPrompt = "Press S to scroll";
    [SerializeField] private List<Material> scenario3Materials = new List<Material>();

    private int currentMaterialIndex = 0;
    private bool isCyclingActive = false;
    private bool isTypingActive = false;
    private bool isLikeScrollActive = false;
    private bool isPostLiked = false;
    private string currentTypedText = "";

    private void Update()
    {
        // Scenario 1
        if (isCyclingActive && Input.GetKeyDown(KeyCode.S))
        {
            AdvanceMaterial();
        }

        // Scenario 2
        if (isTypingActive)
        {
            HandleTyping();
        }

        // Scenario 3
        if (isLikeScrollActive)
        {
            HandleLikeScrollLogic();
        }
    }

    public void TriggerInterruption(int interruptionIndex)
    {
        // Reset all states
        isCyclingActive = false;
        isTypingActive = false;
        isLikeScrollActive = false;
        isPostLiked = false;
        currentMaterialIndex = 0;
        
        if(worldTextDisplay != null) worldTextDisplay.text = "";

        switch (interruptionIndex)
        {
            case 0:
                director.UpdatePromptMessage(scenario1Prompt);
                StartScenario1();
                break;
            case 1:
                director.UpdatePromptMessage(scenario2Prompt + "\"" + targetMessage + "\" and hit Enter");
                StartScenario2();
                break;
            case 2:
                director.UpdatePromptMessage(scenario3LikePrompt);
                StartScenario3();
                break;
        }
    }

    #region Scenario 1: Scrolling
    private void StartScenario1()
    {
        if (screenMaterials.Count == 0) { director.CompleteCurrentPhoneEvent(); return; }
        isCyclingActive = true;
        screenRenderer.material = screenMaterials[0];
    }

    private void AdvanceMaterial()
    {
        currentMaterialIndex++;
        if (currentMaterialIndex < screenMaterials.Count)
            screenRenderer.material = screenMaterials[currentMaterialIndex];
        else
        {
            isCyclingActive = false;
            director.CompleteCurrentPhoneEvent();
        }
    }
    #endregion

    #region Scenario 2: Typing
    private void StartScenario2()
    {
        isTypingActive = true;
        currentTypedText = "";
        if (scenario2ScreenMat != null) screenRenderer.material = scenario2ScreenMat;
        if (worldTextDisplay != null) worldTextDisplay.text = "";
    }

    private void HandleTyping()
    {
        foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (currentTypedText.Length > 0)
                    currentTypedText = currentTypedText.Substring(0, currentTypedText.Length - 1);
            }
            else if (c == '\n' || c == '\r') continue;
            else currentTypedText += c;
        }

        if (worldTextDisplay != null) worldTextDisplay.text = currentTypedText;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SubmitMessage();
        }
    }

    private void SubmitMessage()
    {
        if (currentTypedText.Trim().Equals(targetMessage, System.StringComparison.OrdinalIgnoreCase))
        {
            isTypingActive = false;
            director.CompleteCurrentPhoneEvent();
        }
        else
        {
            // Clear the text if the message was wrong so they can try again
            currentTypedText = "";
            if (worldTextDisplay != null) worldTextDisplay.text = "";
        }
    }
    #endregion

    #region Scenario 3: Like then Scroll
    private void StartScenario3()
    {
        if (scenario3Materials.Count < 2) { director.CompleteCurrentPhoneEvent(); return; }
        isLikeScrollActive = true;
        isPostLiked = false;
        screenRenderer.material = scenario3Materials[0];
    }

    private void HandleLikeScrollLogic()
    {
        // State 1: Need to Like
        if (!isPostLiked)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                isPostLiked = true;
                currentMaterialIndex++; // Move to the "Liked" version of current post
                
                if (currentMaterialIndex < scenario3Materials.Count)
                {
                    screenRenderer.material = scenario3Materials[currentMaterialIndex];
                    director.UpdatePromptMessage(scenario3ScrollPrompt);
                }
            }
        }
        // State 2: Need to Scroll
        else
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                isPostLiked = false;
                currentMaterialIndex++; // Move to the "Unliked" version of NEXT post
                
                if (currentMaterialIndex < scenario3Materials.Count)
                {
                    screenRenderer.material = scenario3Materials[currentMaterialIndex];
                    director.UpdatePromptMessage(scenario3LikePrompt);
                }
                else
                {
                    // No more materials left
                    isLikeScrollActive = false;
                    director.CompleteCurrentPhoneEvent();
                }
            }
        }
    }
    #endregion
}