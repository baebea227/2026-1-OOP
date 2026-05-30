using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerIdentity : NetworkBehaviour
{
    private const int FallbackPlayerNumber = 1;

    [Header("Identity Materials")]
    [SerializeField] private Material[] playerMaterials;

    [Networked] public int PlayerNumber { get; private set; }

    private readonly List<RendererMaterialState> rendererMaterialStates = new List<RendererMaterialState>();
    private int appliedPlayerNumber = int.MinValue;

    private void Awake()
    {
        CacheRendererMaterials();
    }

    public override void Spawned()
    {
        RefreshVisuals();
    }

    public override void Render()
    {
        RefreshVisuals();
    }

    public void Initialize(int playerNumber)
    {
        if (!Object.HasStateAuthority)
            return;

        PlayerNumber = Mathf.Max(FallbackPlayerNumber, playerNumber);
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        int displayNumber = GetDisplayPlayerNumber();
        if (appliedPlayerNumber == displayNumber)
            return;

        ApplyMaterial(displayNumber);
        appliedPlayerNumber = displayNumber;
    }

    private void CacheRendererMaterials()
    {
        rendererMaterialStates.Clear();

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer skinnedRenderer in renderers)
        {
            if (skinnedRenderer == null)
                continue;

            rendererMaterialStates.Add(new RendererMaterialState(
                skinnedRenderer,
                skinnedRenderer.sharedMaterials));
        }
    }

    private void ApplyMaterial(int playerNumber)
    {
        Material material = GetPlayerMaterial(playerNumber);

        foreach (RendererMaterialState state in rendererMaterialStates)
        {
            if (state.Renderer == null)
                continue;

            Material[] materials = state.OriginalMaterials;
            if (materials == null || materials.Length == 0)
                continue;

            Material[] updatedMaterials = new Material[materials.Length];
            materials.CopyTo(updatedMaterials, 0);

            if (material != null)
                updatedMaterials[0] = material;

            state.Renderer.sharedMaterials = updatedMaterials;
        }
    }

    private Material GetPlayerMaterial(int playerNumber)
    {
        int materialIndex = playerNumber - 1;
        if (playerMaterials == null || materialIndex < 0 || materialIndex >= playerMaterials.Length)
            return null;

        return playerMaterials[materialIndex];
    }

    private int GetDisplayPlayerNumber()
    {
        return PlayerNumber > 0 ? PlayerNumber : FallbackPlayerNumber;
    }

    private readonly struct RendererMaterialState
    {
        public RendererMaterialState(Renderer renderer, Material[] originalMaterials)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials;
        }

        public Renderer Renderer { get; }
        public Material[] OriginalMaterials { get; }
    }
}
