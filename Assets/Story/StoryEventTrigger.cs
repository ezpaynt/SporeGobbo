using System;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class StoryEventTrigger : MonoBehaviour
{
    private Func<bool> eligibility;
    private Func<bool> begin;
    private bool started;

    public void Configure(float radius, Func<bool> eligibilityPolicy, Func<bool> beginEvent)
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.1f, radius);
        eligibility = eligibilityPolicy;
        begin = beginEvent;
    }

    public bool TryOffer()
    {
        if (started || eligibility == null || begin == null || !eligibility()) return false;
        if (!begin()) return false;
        started = true;
        return true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player")) TryOffer();
    }
}
