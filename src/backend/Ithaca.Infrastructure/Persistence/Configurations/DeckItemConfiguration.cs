using Ithaca.Domain.Decks;
using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>deck_items</c>: cards in a deck. A film appears once per deck, a position once per deck (both unique). The film FK is
/// <c>ON DELETE RESTRICT</c> like <c>user_films</c> (ADR-0019): the sync refreshes referenced films instead of purging them.
/// </summary>
internal sealed class DeckItemConfiguration : IEntityTypeConfiguration<DeckItem>
{
    public void Configure(EntityTypeBuilder<DeckItem> builder)
    {
        builder.ToTable("deck_items", t =>
            t.HasCheckConstraint("ck_deck_items_position", $"position BETWEEN 1 AND {Deck.MaxItems}"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.HasOne<Deck>().WithMany().HasForeignKey(i => i.DeckId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Film>().WithMany().HasForeignKey(i => i.FilmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.DeckId, i.Position }).IsUnique().HasDatabaseName("ux_deck_items_deck_id_position");
        builder.HasIndex(i => new { i.DeckId, i.FilmId }).IsUnique().HasDatabaseName("ux_deck_items_deck_id_film_id");
        builder.HasIndex(i => i.FilmId).HasDatabaseName("ix_deck_items_film_id");
    }
}
