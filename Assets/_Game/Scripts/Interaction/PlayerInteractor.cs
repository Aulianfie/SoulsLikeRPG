using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private InteractionPromptUI _promptUI;
    [SerializeField, Min(0.1f)] private float _maxInteractionDistance = 3.5f;

    private readonly List<MonoBehaviour> _nearby = new List<MonoBehaviour>();
    private PlayerInputReader _inputReader;
    private IInteractable _shownTarget;
    private string _shownText;

    private PlayerStateMachine _stateMachine;

    public float MaxInteractionDistance => _maxInteractionDistance;

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void Update()
    {
        if (!_inputReader.isActiveAndEnabled)
        {
            if (_shownTarget != null)
                _promptUI?.Hide();
            _shownTarget = null;
            _shownText = null;
            return;
        }

        IInteractable closest = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = _nearby.Count - 1; i >= 0; i--)
        {
            MonoBehaviour behaviour = _nearby[i];
            if (behaviour == null || !behaviour.isActiveAndEnabled)
            {
                _nearby.RemoveAt(i);
                continue;
            }

            IInteractable interactable = (IInteractable)behaviour;
            if (!interactable.CanInteract)
                continue;

            float sqrDistance = (behaviour.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > _maxInteractionDistance * _maxInteractionDistance)
                continue;
            if (sqrDistance < closestSqrDistance)
            {
                closest = interactable;
                closestSqrDistance = sqrDistance;
            }
        }

        string promptText = closest != null ? closest.InteractionText : null;
        if (_promptUI != null && (closest != _shownTarget || promptText != _shownText))
        {
            if (closest == null)
                _promptUI.Hide();
            else
                _promptUI.Show(promptText);

            _shownTarget = closest;
            _shownText = promptText;
        }

        if (_inputReader.ConsumeInteract() && closest != null)
        {
            // 申请播放交互动作，实际效果由 InteractState 在动作完成后执行。
            _stateMachine.TryBeginInteraction(
                closest,
                _maxInteractionDistance
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.isTrigger)
            return;

        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IInteractable && !_nearby.Contains(behaviour))
                _nearby.Add(behaviour);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.isTrigger)
            return;

        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
            _nearby.Remove(behaviour);
    }

    private void OnDisable()
    {
        _nearby.Clear();
        _shownTarget = null;
        _shownText = null;
        if (_promptUI != null)
            _promptUI.Hide();
    }
}
