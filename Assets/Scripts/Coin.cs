using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : Colletables
{
    [SerializeField] private ParticleSystem _coinParticle1;
    [SerializeField] private ParticleSystem _coinParticle2;

    private BoxCollider _collider;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        _collider = gameObject.GetComponent<BoxCollider>();
        itemPoints = 1;
    }

    protected override void OnTriggerEnter(Collider other)
    {
        CollectItem();
        _coinParticle1.Stop();
        _coinParticle2.Play();
        _collider.enabled = false;
        StartCoroutine(DestroyObject());
    }

    protected override IEnumerator DestroyObject()
    {
        yield return new WaitForSeconds(0.6f);
        Destroy(gameObject);
    }
}
