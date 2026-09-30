using System.Collections.Generic;
using UnityEngine;

public enum MagneticPole
{
    None,
    Positive,
    Negative
}

[RequireComponent(typeof(BoxCollider2D))]
public class MagneticZone : MonoBehaviour
{
    [Header("레이어별 자극 설정")]
    [Tooltip("+극으로 사용할 레이어입니다. 기본값은 middle입니다.")]
    [SerializeField] private LayerMask positivePoleLayers = 1 << 6;

    [Tooltip("-극으로 사용할 레이어입니다. 기본값은 Player입니다.")]
    [SerializeField] private LayerMask negativePoleLayers = 1 << 13;

    private readonly Dictionary<AutoRunner, int> contactCounts = new Dictionary<AutoRunner, int>();

    private void Reset()
    {
        positivePoleLayers = MaskForLayer("middle");
        negativePoleLayers = MaskForLayer("Player");
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        AutoRunner runner = other.GetComponentInParent<AutoRunner>();
        if (runner == null || !AreOppositePoles(gameObject.layer, runner.gameObject.layer))
            return;

        contactCounts.TryGetValue(runner, out int count);
        contactCounts[runner] = count + 1;

        if (count == 0)
            runner.SetMagnetized(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        AutoRunner runner = other.GetComponentInParent<AutoRunner>();
        if (runner == null || !contactCounts.TryGetValue(runner, out int count))
            return;

        if (count > 1)
        {
            contactCounts[runner] = count - 1;
            return;
        }

        contactCounts.Remove(runner);
        runner.SetMagnetized(false);
    }

    private void OnDisable()
    {
        foreach (AutoRunner runner in contactCounts.Keys)
        {
            if (runner != null)
                runner.SetMagnetized(false);
        }

        contactCounts.Clear();
    }

    private bool AreOppositePoles(int firstLayer, int secondLayer)
    {
        MagneticPole firstPole = GetPole(firstLayer);
        MagneticPole secondPole = GetPole(secondLayer);

        return (firstPole == MagneticPole.Positive && secondPole == MagneticPole.Negative)
            || (firstPole == MagneticPole.Negative && secondPole == MagneticPole.Positive);
    }

    private MagneticPole GetPole(int layer)
    {
        int layerBit = 1 << layer;

        if ((positivePoleLayers.value & layerBit) != 0)
            return MagneticPole.Positive;

        if ((negativePoleLayers.value & layerBit) != 0)
            return MagneticPole.Negative;

        return MagneticPole.None;
    }

    private static LayerMask MaskForLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer < 0 ? 0 : 1 << layer;
    }
}
