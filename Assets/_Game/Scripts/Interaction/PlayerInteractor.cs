using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private InteractionPromptUI _promptUI;

    private readonly List<MonoBehaviour> _nearby = new List<MonoBehaviour>();
    private PlayerInputReader _inputReader;
    private IInteractable _currentTarget;

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
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
            if (sqrDistance < closestSqrDistance)
            {
                closest = interactable;
                closestSqrDistance = sqrDistance;
            }
        }

        _currentTarget = closest;
        if (_promptUI != null)
        {
            if (closest == null)
                _promptUI.Hide();
            else
                _promptUI.Show(closest.InteractionText);
        }

        if (_inputReader.ConsumeInteract() && closest != null && closest.CanInteract)
            closest.Interact();
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
        _currentTarget = null;
        if (_promptUI != null)
            _promptUI.Hide();
    }
}
