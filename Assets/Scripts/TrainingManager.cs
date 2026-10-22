using UnityEngine;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class ProcedureStep
{
    public string stepId;
    public string instruction;
}

public class TrainingManager : MonoBehaviour
{
    public static TrainingManager Instance;

    public List<ProcedureStep> steps = new List<ProcedureStep>();
    public TMP_Text instructionText;
    private int currentStepIndex = 0;

    public string CurrentStepId => (currentStepIndex < steps.Count) ? steps[currentStepIndex].stepId : null;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ShowCurrentStep();
    }

    public void OnButtonPressed(string buttonId)
    {
        if (currentStepIndex >= steps.Count) return;

        if (steps[currentStepIndex].stepId == buttonId)
        {
            currentStepIndex++;
            ShowCurrentStep();
        }
    }

    void ShowCurrentStep()
    {
        if (instructionText == null) return;

        if (currentStepIndex < steps.Count)
            instructionText.text = "Paso " + (currentStepIndex + 1) + ": " + steps[currentStepIndex].instruction;
        else
            instructionText.text = "¡Procedimiento completado!";
    }
}