using System.Collections;
using UnityEngine;

public sealed class Day0AnimationTester : MonoBehaviour
{
    public Animator Animator;
    public AnimationClip WeaponSlotClip;
    public AnimationClip[] WeaponTestClips;
    public bool AutoPlayValidation;
    public float ValidationStepSeconds = 1.25f;

    private AnimatorOverrideController _overrideController;
    private int _weaponIndex;
    private string _currentState = "Idle";

    private void Awake()
    {
        if (Animator == null)
        {
            Animator = GetComponent<Animator>();
        }

        _overrideController = new AnimatorOverrideController(Animator.runtimeAnimatorController);
        Animator.runtimeAnimatorController = _overrideController;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlayState("Idle");
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayState("Walk");
        if (Input.GetKeyDown(KeyCode.Alpha3)) PlayState("Run");
        if (Input.GetKeyDown(KeyCode.Alpha4)) PlayState("Roll");
        if (Input.GetKeyDown(KeyCode.Alpha5)) PlayState("Hit");
        if (Input.GetKeyDown(KeyCode.Alpha6)) PlayState("Death");
        if (Input.GetKeyDown(KeyCode.Space)) PlayNextWeaponClip();
    }

    private IEnumerator Start()
    {
        if (!AutoPlayValidation)
        {
            yield break;
        }

        yield return null;
        string[] states = { "Idle", "Walk", "Run", "Roll", "Hit", "Death" };
        for (int i = 0; i < states.Length; i++)
        {
            string state = states[i];
            PlayState(state);
            Debug.Log($"[Day0 Validation] Playing state: {state}", this);
            yield return new WaitForSeconds(ValidationStepSeconds);
        }

        for (int i = 0; i < WeaponTestClips.Length; i++)
        {
            PlayNextWeaponClip();
            Debug.Log($"[Day0 Validation] Playing attack: {WeaponTestClips[i].name}", this);
            yield return new WaitForSeconds(ValidationStepSeconds);
        }

        PlayState("Idle");
        Debug.Log("[Day0 Validation] Automatic animation sequence completed (6 states + 6 attacks).", this);
    }

    private void PlayState(string stateName)
    {
        _currentState = stateName;
        Animator.Play(stateName, 0, 0f);
    }

    private void PlayNextWeaponClip()
    {
        if (WeaponSlotClip == null || WeaponTestClips == null || WeaponTestClips.Length == 0)
        {
            return;
        }

        AnimationClip clip = WeaponTestClips[_weaponIndex % WeaponTestClips.Length];
        _weaponIndex++;
        _overrideController[WeaponSlotClip.name] = clip;
        _currentState = $"WeaponAttackTest: {clip.name}";
        Animator.Play("WeaponAttackTest", 0, 0f);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(12, 12, 430, 72), "Day0 Animation Lab");
        GUI.Label(new Rect(24, 36, 400, 20), "1 Idle  2 Walk  3 Run  4 Roll  5 Hit  6 Death  Space Attack");
        GUI.Label(new Rect(24, 58, 400, 20), $"Current: {_currentState}");
    }
}
