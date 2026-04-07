using Microsoft.Extensions.Logging;
using Training.Application.Services;
using Training.Domain.Enums;

namespace Training.Infrastructure.Seeders;

/// <summary>
/// Seeds the skills catalog with common gymnastics skills for each section.
/// Can be run independently or as part of application startup.
/// </summary>
public sealed class SkillSeeder
{
    private readonly ISkillService _skillService;
    private readonly ILogger<SkillSeeder> _logger;

    public SkillSeeder(ISkillService skillService, ILogger<SkillSeeder> logger)
    {
        _skillService = skillService;
        _logger = logger;
    }

    /// <summary>
    /// Seeds all gymnastics skills. Idempotent - can be run multiple times safely.
    /// </summary>
    public async Task SeedAsync(Guid systemTenantId, Guid systemUserId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting skills catalog seeding...");

        var skills = GetSkillSeedData();
        var seededCount = 0;
        var skippedCount = 0;

        foreach (var (title, description, rating, sections) in skills)
        {
            // Check if skill already exists by title
            var searchResult = await _skillService.SearchAsync(title, maxResults: 1, cancellationToken: cancellationToken);

            if (searchResult.IsSuccess && searchResult.Value!.Any(r => r.Skill.Title.Equals(title, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogDebug("Skill '{Title}' already exists, skipping", title);
                skippedCount++;
                continue;
            }

            var result = await _skillService.CreateAsync(
                title,
                description,
                rating,
                sections,
                systemTenantId,
                systemUserId,
                imageUrl: null,
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Seeded skill: {Title} (Sections: {Sections})",
                    title, string.Join(", ", sections));
                seededCount++;
            }
            else
            {
                _logger.LogWarning("Failed to seed skill '{Title}': {Error}", title, result.ErrorMessage);
            }
        }

        _logger.LogInformation("Skills seeding completed. Seeded: {SeededCount}, Skipped: {SkippedCount}",
            seededCount, skippedCount);
    }

    private static List<(string Title, string Description, int Rating, IReadOnlyList<GymnasticSection> Sections)> GetSkillSeedData()
    {
        return new List<(string, string, int, IReadOnlyList<GymnasticSection>)>
        {
            // BARS SKILLS
            (
                "Pull-ups",
                "Hang from bar with arms fully extended, pull body up until chin is over the bar, lower with control. Builds upper body strength essential for bars work.",
                5,
                new[] { GymnasticSection.Bars, GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Kip",
                "Fundamental bars skill using momentum to transition from hanging to support position. Swing legs forward and back, then drive hips to bar while pulling with arms.",
                4,
                new[] { GymnasticSection.Bars }.ToList()
            ),
            (
                "Cast to Handstand",
                "From front support on bars, swing legs forward then forcefully backward and up to reach handstand position. Requires shoulder flexibility and core control.",
                5,
                new[] { GymnasticSection.Bars }.ToList()
            ),
            (
                "Giant Swing",
                "Complete 360-degree rotation around the bar while maintaining body tension. Advanced skill requiring significant strength and technique.",
                5,
                new[] { GymnasticSection.Bars }.ToList()
            ),
            (
                "Clear Hip Circle",
                "From front support, pike hips away from bar then circle around to return to support. Key progression skill for bars.",
                4,
                new[] { GymnasticSection.Bars }.ToList()
            ),

            // FLOOR SKILLS
            (
                "Forward Roll",
                "Fundamental tumbling skill. Tuck chin, roll forward along spine from shoulders to hips. Essential foundation for all floor work.",
                3,
                new[] { GymnasticSection.Floor }.ToList()
            ),
            (
                "Backward Roll",
                "Roll backward with control, pushing through hands near ears to stand. Builds spatial awareness and back tumbling foundation.",
                3,
                new[] { GymnasticSection.Floor }.ToList()
            ),
            (
                "Cartwheel",
                "Sideways rotation placing hands on floor one at a time. Fundamental for developing body awareness and lateral movement.",
                3,
                new[] { GymnasticSection.Floor }.ToList()
            ),
            (
                "Round-off",
                "Similar to cartwheel but landing on both feet simultaneously. Critical entry skill for back tumbling sequences.",
                4,
                new[] { GymnasticSection.Floor }.ToList()
            ),
            (
                "Back Handspring",
                "Jump backward onto hands then snap down to feet. Essential backward tumbling skill requiring power and technique.",
                5,
                new[] { GymnasticSection.Floor }.ToList()
            ),
            (
                "Front Handspring",
                "Run, hurdle, block through hands to land on feet. Forward tumbling power skill.",
                4,
                new[] { GymnasticSection.Floor }.ToList()
            ),
            (
                "Handstand",
                "Hold inverted vertical position on hands for 30+ seconds. Foundational for almost all gymnastics skills.",
                5,
                new[] { GymnasticSection.Floor, GymnasticSection.Bars, GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Bridge",
                "Back flexibility position with hands and feet on floor, body arched. Essential for back flexibility development.",
                3,
                new[] { GymnasticSection.Floor, GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Split Leap",
                "Jump into airborne split position with legs 180 degrees apart. Requires flexibility and power.",
                4,
                new[] { GymnasticSection.Floor, GymnasticSection.Beam }.ToList()
            ),
            (
                "Back Tuck",
                "Back flip with knees tucked to chest. First backward aerial rotation skill.",
                5,
                new[] { GymnasticSection.Floor }.ToList()
            ),

            // BEAM SKILLS
            (
                "Beam Walk",
                "Walk forward on beam maintaining balance and posture. Fundamental beam skill building confidence.",
                2,
                new[] { GymnasticSection.Beam }.ToList()
            ),
            (
                "Beam Turns (180° and 360°)",
                "Execute controlled turns on one foot while maintaining balance. Progress from half turns to full turns.",
                3,
                new[] { GymnasticSection.Beam }.ToList()
            ),
            (
                "Handstand on Beam",
                "Hold handstand on 4-inch beam surface. Requires exceptional balance and body control.",
                5,
                new[] { GymnasticSection.Beam }.ToList()
            ),
            (
                "Cartwheel on Beam",
                "Perform cartwheel along beam length maintaining straight line. Challenging balance and spatial awareness skill.",
                4,
                new[] { GymnasticSection.Beam }.ToList()
            ),
            (
                "Back Walkover on Beam",
                "Kick to handstand and continue through to feet while staying on beam. Combines flexibility and balance.",
                5,
                new[] { GymnasticSection.Beam }.ToList()
            ),
            (
                "Leap Series on Beam",
                "Perform consecutive split leaps or jumps along beam. Requires rhythm, power, and precise landings.",
                4,
                new[] { GymnasticSection.Beam }.ToList()
            ),
            (
                "Scale Hold",
                "Balance on one leg with other leg held in arabesque or similar position for 2+ seconds. Builds stability.",
                3,
                new[] { GymnasticSection.Beam, GymnasticSection.Floor }.ToList()
            ),

            // VAULT/RANGE SKILLS
            (
                "Vault Run and Hurdle",
                "Sprint approach accelerating into hurdle step onto springboard. Foundation for all vault skills.",
                3,
                new[] { GymnasticSection.RangeVault }.ToList()
            ),
            (
                "Handstand Flat Back",
                "Block through handstand on vault table, fall flat to landing mat. Teaches blocking technique.",
                3,
                new[] { GymnasticSection.RangeVault }.ToList()
            ),
            (
                "Front Handspring Vault",
                "Block through handstand position and push off table to land on feet. First flight vault.",
                4,
                new[] { GymnasticSection.RangeVault }.ToList()
            ),
            (
                "Tsukahara",
                "Quarter turn onto vault table followed by back flip off. Advanced twisting vault.",
                5,
                new[] { GymnasticSection.RangeVault }.ToList()
            ),
            (
                "Yurchenko",
                "Round-off onto springboard, back handspring onto table, back flip off. One of most common competitive vaults.",
                5,
                new[] { GymnasticSection.RangeVault }.ToList()
            ),

            // STRENGTH & CONDITIONING
            (
                "Hollow Body Hold",
                "Lie on back, press lower back to floor, raise shoulders and legs slightly off ground. Core strength foundation.",
                5,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Arch Body Hold",
                "Lie on stomach, raise chest and legs off floor creating arch shape. Back strength and posterior chain development.",
                4,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Plank Hold",
                "Hold pushup position on forearms for 30-60 seconds. Core endurance and stability.",
                4,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Pike Compression Lifts",
                "Sit in pike position, lift hips off floor using only abdominal compression. Advanced core skill.",
                5,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Leg Lifts",
                "Hang from bar or lie on back, lift straight legs to 90 degrees with control. Lower body strength and control.",
                4,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Shoulder Shrugs",
                "Hang from bar and shrug shoulders up towards ears without bending arms. Builds scapular strength for bars.",
                4,
                new[] { GymnasticSection.Bars, GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "L-Sit Hold",
                "Hold body in L-shape with legs parallel to floor while supporting on hands. Advanced core and hip flexor strength.",
                5,
                new[] { GymnasticSection.StrengthConditioning, GymnasticSection.Bars }.ToList()
            ),
            (
                "Wall Slides",
                "Stand against wall, slide down into squat position. Develops shoulder mobility and stability.",
                3,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Pistol Squats",
                "Single leg squat lowering until hamstring touches calf, other leg extended forward. Unilateral leg strength.",
                5,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Box Jumps",
                "Jump onto elevated surface from standing position. Develops explosive power for tumbling.",
                4,
                new[] { GymnasticSection.StrengthConditioning, GymnasticSection.Floor }.ToList()
            ),
            (
                "Resistance Band Work",
                "Use resistance bands for shoulder strengthening, rotation exercises, and injury prevention. Essential supplementary training.",
                3,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            ),
            (
                "Wrist Conditioning",
                "Various wrist stretches, strengthening, and mobility exercises. Critical for preventing wrist injuries.",
                4,
                new[] { GymnasticSection.StrengthConditioning, GymnasticSection.Floor, GymnasticSection.Bars }.ToList()
            ),
            (
                "Ankle Strengthening",
                "Point and flex exercises, calf raises, ankle circles. Builds ankle stability for landings.",
                3,
                new[] { GymnasticSection.StrengthConditioning }.ToList()
            )
        };
    }
}
