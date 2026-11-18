using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class CalibrationResultUI : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("Data Source")]

    [SerializeField]
    private CalibrationResultController resultController;


    [Header("Text References")]

    [SerializeField]
    private TMP_Text resultStatusText;


    [SerializeField]
    private TMP_Text resultMetricsText;


    [Header("Button References")]

    [SerializeField]
    private Button acceptButton;


    [SerializeField]
    private Button recalibrateButton;


    [SerializeField]
    private Button continueAnywayButton;


    // ============================================================
    // PUBLIC METHOD
    // ============================================================

    public void RefreshUI()
    {
        if (resultController == null)
        {
            Debug.LogWarning(
                "CalibrationResultUI: Result Controller is not assigned."
            );

            return;
        }


        UpdateStatus();

        UpdateMetrics();

        UpdateButtons();
    }


    // ============================================================
    // STATUS
    // ============================================================

    private void UpdateStatus()
    {
        if (resultStatusText == null)
        {
            return;
        }


        string result =
            resultController.ValidationResult;


        if (result == "PASS")
        {
            resultStatusText.text =
                "PASS";
        }
        else if (result == "RECALIBRATION RECOMMENDED")
        {
            resultStatusText.text =
                "RECALIBRATION RECOMMENDED";
        }
        else
        {
            resultStatusText.text =
                "NOT RUN";
        }
    }


    // ============================================================
    // METRICS
    // ============================================================

    private void UpdateMetrics()
    {
        if (resultMetricsText == null)
        {
            return;
        }


        float mean =
            resultController.MeanAngularError;


        float median =
            resultController.MedianAngularError;


        float maximum =
            resultController.MaximumAngularError;


        float valid =
            resultController.ValidSamplePercentage;


        string meanText =
            float.IsNaN(mean)
            ? "--"
            : mean.ToString("F2");


        string medianText =
            float.IsNaN(median)
            ? "--"
            : median.ToString("F2");


        string maximumText =
            float.IsNaN(maximum)
            ? "--"
            : maximum.ToString("F2");


        string validText =
            valid.ToString("F1");


        resultMetricsText.text =
            "Mean angular error: "
            + meanText
            + "°\n"
            + "Median angular error: "
            + medianText
            + "°\n"
            + "Maximum angular error: "
            + maximumText
            + "°\n"
            + "Valid gaze samples: "
            + validText
            + "%";
    }


    // ============================================================
    // BUTTON STATE
    // ============================================================

    private void UpdateButtons()
    {
        string result =
            resultController.ValidationResult;


        if (result == "PASS")
        {
            if (acceptButton != null)
            {
                acceptButton.interactable =
                    true;
            }


            if (recalibrateButton != null)
            {
                recalibrateButton.interactable =
                    true;
            }


            if (continueAnywayButton != null)
            {
                continueAnywayButton.interactable =
                    false;
            }
        }
        else if (result == "RECALIBRATION RECOMMENDED")
        {
            if (acceptButton != null)
            {
                acceptButton.interactable =
                    false;
            }


            if (recalibrateButton != null)
            {
                recalibrateButton.interactable =
                    true;
            }


            if (continueAnywayButton != null)
            {
                continueAnywayButton.interactable =
                    true;
            }
        }
        else
        {
            if (acceptButton != null)
            {
                acceptButton.interactable =
                    false;
            }


            if (recalibrateButton != null)
            {
                recalibrateButton.interactable =
                    false;
            }


            if (continueAnywayButton != null)
            {
                continueAnywayButton.interactable =
                    false;
            }
        }
    }


    // ============================================================
    // UNITY
    // ============================================================

    private void Start()
    {
        RefreshUI();
    }
}