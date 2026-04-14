namespace ClikerSlash.Battle
{
    /// <summary>
    /// 전투 시각 자산을 어드레서블 친화적인 논리 키로 식별합니다.
    /// </summary>
    public static class BattleAssetKeys
    {
        public const string PlayerView = "battle/player_view";
        public const string CargoView = "battle/cargo_view";
        public const string StandardCargoView = "battle/cargo_view_standard";
        public const string GeneralCargoView = StandardCargoView;
        public const string FragileCargoView = "battle/cargo_view_fragile";
        public const string HeavyCargoView = "battle/cargo_view_heavy";
        public const string FrozenCargoView = HeavyCargoView;
        public const string PlayerMaterial = "battle/materials/player_cube";
        public const string CargoMaterial = "battle/materials/cargo_cube";
        public const string StandardCargoMaterial = "battle/materials/cargo_cube_standard";
        public const string GeneralCargoMaterial = StandardCargoMaterial;
        public const string FragileCargoMaterial = "battle/materials/cargo_cube_fragile";
        public const string HeavyCargoMaterial = "battle/materials/cargo_cube_heavy";
        public const string FrozenCargoMaterial = HeavyCargoMaterial;
        public const string LaneMaterial = "battle/materials/lane_strip";
        public const string AccentMaterial = "battle/materials/line_accent";
    }

    /// <summary>
    /// 논리 키를 실제 런타임 로드 식별자로 해석하는 얇은 계약입니다.
    /// </summary>
    public interface IBattleAssetReferenceProvider
    {
        string GetReferenceId(string assetKey);
    }
}
