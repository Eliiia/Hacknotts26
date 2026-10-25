using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PlayerMovement), typeof(PlayerAttack), typeof(PlayerRangedAttack))]
public class GuitarPlayerControls : MonoBehaviour
{
    public GuitarInput guitarInput;
    [Header("Action notes (case sensitive)")]
    public string jumpNote = "G0";
    public string primaryAttackNote = "B0";
    public string secondaryAttackNote = "";
    public string rangedAttackNote = "e0";
    [Header("Note filtering")]
    [Min(0f)] public float stableNoteSeconds = 0.06f;
    [Min(0f)] public float releaseSeconds = 0.12f;

    private PlayerMovement movement;
    private PlayerAttack melee;
    private PlayerRangedAttack ranged;
    private string candidate, lastTriggered;
    private float candidateSince, silenceSince = -1f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        melee = GetComponent<PlayerAttack>();
        ranged = GetComponent<PlayerRangedAttack>();
        if (guitarInput == null) guitarInput = FindFirstObjectByType<GuitarInput>();
    }

    private void Update()
    {
        if (guitarInput == null || !guitarInput.isActiveAndEnabled ||
            !guitarInput.CaptureReady || guitarInput.Calibrating)
        {
            ResetNotes();
            return;
        }
        ProcessNote(guitarInput.GetCurrentNote(), Time.unscaledTime);
    }

    private void ProcessNote(string note, float now)
    {
        if (string.IsNullOrEmpty(note) || note == "??")
        {
            candidate = null;
            if (silenceSince < 0f) silenceSince = now;
            if (now - silenceSince >= releaseSeconds) lastTriggered = null;
            return;
        }
        silenceSince = -1f;
        if (candidate != note)
        {
            candidate = note;
            candidateSince = now;
        }
        if (now - candidateSince < stableNoteSeconds || note == lastTriggered) return;
        lastTriggered = note;
        TriggerNote(note);
    }

    private void TriggerNote(string note)
    {
        // E1..E12 use exactly the keyboard slot path, including ranged targeting.
        if (note.Length > 1 && note[0] == 'E' &&
            int.TryParse(note.Substring(1), out int slot) && slot >= 1 && slot <= PlayerMovement.SlotCount)
        {
            movement.RequestGuitarSlot(slot - 1);
            return;
        }
        if (note == jumpNote) movement.RequestJump();
        else if (note == primaryAttackNote) melee.PrimaryAttack();
        else if (note == secondaryAttackNote) melee.SecondaryAttack();
        else if (note == rangedAttackNote) ranged.ToggleAimAndFire();
    }

    private void ResetNotes()
    {
        candidate = lastTriggered = null;
        silenceSince = -1f;
    }

    private void OnDisable() { ResetNotes(); }
}
