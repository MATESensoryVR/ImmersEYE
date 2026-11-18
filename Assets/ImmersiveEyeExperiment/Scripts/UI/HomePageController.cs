using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class HomePageController : MonoBehaviour
{
    [Header("Input Fields")]

    [SerializeField]
    private TMP_InputField participantIDInput;

    [SerializeField]
    private TMP_InputField sessionIDInput;


    [Header("Consent")]

    [SerializeField]
    private Toggle consentToggle;


    [Header("Start Button")]

    [SerializeField]
    private Button startSessionButton;


    private void Start()
    {
        if (participantIDInput != null)
        {
            participantIDInput.onValueChanged.AddListener(
                OnTextChanged
            );
        }

        if (sessionIDInput != null)
        {
            sessionIDInput.onValueChanged.AddListener(
                OnTextChanged
            );
        }

        if (consentToggle != null)
        {
            consentToggle.onValueChanged.AddListener(
                OnConsentChanged
            );

            consentToggle.isOn = false;
        }

        UpdateStartButton();
    }


    private void OnTextChanged(string value)
    {
        UpdateStartButton();
    }


    private void OnConsentChanged(bool value)
    {
        UpdateStartButton();
    }


    private void UpdateStartButton()
    {
        if (startSessionButton == null)
        {
            return;
        }

        bool participantEntered =
            participantIDInput != null
            &&
            !string.IsNullOrWhiteSpace(
                participantIDInput.text
            );

        bool sessionEntered =
            sessionIDInput != null
            &&
            !string.IsNullOrWhiteSpace(
                sessionIDInput.text
            );

        bool consentGiven =
            consentToggle != null
            &&
            consentToggle.isOn;

        startSessionButton.interactable =
            participantEntered
            &&
            sessionEntered
            &&
            consentGiven;
    }


    public string GetParticipantID()
    {
        if (participantIDInput == null)
        {
            return "";
        }

        return participantIDInput.text.Trim();
    }


    public string GetSessionID()
    {
        if (sessionIDInput == null)
        {
            return "";
        }

        return sessionIDInput.text.Trim();
    }


    public bool HasConsent()
    {
        return consentToggle != null
               &&
               consentToggle.isOn;
    }
}