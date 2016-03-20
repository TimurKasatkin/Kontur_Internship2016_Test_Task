using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace KonturInternshipTeskTask
{
    public class Hanabi
    {
        #region turn description' templates and appropriate actions

        private static bool NotExistsOrOver(Game game)
        {
            return game?.Over ?? true;
        }

        private static bool ExistsAndNotOver(Game game)
        {
            return !game?.Over ?? false;
        }

        private static readonly Regex StartGameCommandRegex =
            new Regex("^Start new game with deck (?<deck>((?<card>[RGBWY][1-5]) ?){11,})$");

        private static Game HandleStartGameCommand(string turnDescription, Game currentGame)
        {
            string[] cardDescriptions = StartGameCommandRegex.Match(turnDescription).Groups["deck"].Value.Split();
            var player1Cards = cardDescriptions.Take(5).Select(CardUtils.ParseCard).ToList();
            var player2Cards = cardDescriptions.Skip(5).Take(5).Select(CardUtils.ParseCard).ToList();
            var deck = cardDescriptions.Skip(10).Select(CardUtils.ParseCard).ToList();
            return new Game(new Player(player1Cards), new Player(player2Cards), deck);
        }

        private static readonly Regex PlayCardCommandRegex = new Regex(@"Play card (?<card_index>\d+)");

        private static Game HandlePlayCardCommand(string turnDescription, Game currentGame)
        {
            if (ExistsAndNotOver(currentGame))
            {
                var cardIndex = ushort.Parse(PlayCardCommandRegex.Match(turnDescription).Groups["card_index"].Value);
                currentGame.PlayCard(cardIndex);
            }

            return currentGame;
        }

        private static readonly Regex DropCardCommandRegex = new Regex(@"Drop card (?<card_index>\d+)");

        private static Game HandleDropCardCommand(string turnDescription, Game currentGame)
        {
            if (ExistsAndNotOver(currentGame))
            {
                var cardIndex = ushort.Parse(DropCardCommandRegex.Match(turnDescription).Groups["card_index"].Value);
                currentGame.DropCard(cardIndex);
            }

            return currentGame;
        }

        private static readonly Regex TellColorCommandRegex = new Regex(
            @"Tell color (?<proposed_color>Red|Green|Blue|White|Yellow) for cards (?<card_indexes>(\d+ ?)+)");

        private static Game HandleTellColorCommand(string turnDescription, Game currentGame)
        {
            if (ExistsAndNotOver(currentGame))
            {
                var match = TellColorCommandRegex.Match(turnDescription);
                var proposedColor = match.Groups["proposed_color"].Value.ParseColor().Value;
                var cardIndexes = match.Groups["card_indexes"].Value.Split().Select(ushort.Parse).ToList();
                currentGame.TellColor(proposedColor, cardIndexes);
            }

            return currentGame;
        }

        private static readonly Regex TellRankCommandRegex =
            new Regex(@"Tell rank (?<card_rank>[1-5]) for cards (?<card_indexes>(\d+ ?)+)");

        private static Game HandleTellRankCommand(string turnDescription, Game currentGame)
        {
            if (ExistsAndNotOver(currentGame))
            {
                var match = TellRankCommandRegex.Match(turnDescription);
                var proposedRank = match.Groups["card_rank"].Value.ParseRank();
                var cardIndexes = match.Groups["card_indexes"].Value.Split().Select(ushort.Parse).ToList();
                currentGame.TellRank(proposedRank, cardIndexes);
            }

            return currentGame;
        }

        /// <summary>
        /// Key - command template (regexp)
        /// Value - function, which accepts turn description as string, current game 
        /// and returns new game (if start was requested) or the same game
        /// </summary>
        private static readonly Dictionary<Regex, Func<string, Game, Game>> CommandActionDict =
            new Dictionary<Regex, Func<string, Game, Game>>
            {
                {StartGameCommandRegex, HandleStartGameCommand},
                {PlayCardCommandRegex, HandlePlayCardCommand},
                {DropCardCommandRegex, HandleDropCardCommand},
                {TellColorCommandRegex, HandleTellColorCommand},
                {TellRankCommandRegex, HandleTellRankCommand}
            };

        private static Func<string, Game, Game> GetTurnBy(string turnDescription)
        {
            return CommandActionDict.FirstOrDefault(
                turnRegexActionPair => turnRegexActionPair.Key.IsMatch(turnDescription ?? "")).Value;
        }

        #endregion

        #region current flow of games

        private Game _currentGame;
        public bool CurrentGameExists => _currentGame != null;
        public bool CurrentGameOver => _currentGame.Over;

        public void Play()
        {
            string turnDescription;
            while ((turnDescription = Console.ReadLine()) != null)
            {
                _currentGame = GetTurnBy(turnDescription)?.Invoke(turnDescription, _currentGame) ?? _currentGame;
                if (CurrentGameExists && CurrentGameOver)
                {
                    Console.WriteLine("Turn: {0}, cards: {1}, with risk: {2}", _currentGame.NumberOfTurns,
                        _currentGame.NumOfCorrectlyPlayedCards, _currentGame.NumOfRiskyTurns);
                    _currentGame = null;
                }
            }
        }

        #endregion

        public static void Main(string[] args)
        {
            new Hanabi().Play();
        }
    }

    internal class Game
    {
        public uint NumberOfTurns { get; private set; }

        public uint NumOfCorrectlyPlayedCards { get; private set; }

        public uint NumOfRiskyTurns { get; private set; }

        private bool _over = false;
        public bool Over => _over || TableHasMaxNumberOfCards() || _deck.Count == 0;

        private readonly List<Card> _deck;

        private readonly Dictionary<CardColor, ushort> _table = new Dictionary<CardColor, ushort>
        {
            {CardColor.Red, 0}, //value is a max rank for card of particular color on table
            {CardColor.Green, 0},
            {CardColor.Blue, 0},
            {CardColor.White, 0},
            {CardColor.Yellow, 0},
        };

        private readonly Player _player1, _player2;

        private Player _currentPlayer;
        private Player _otherPlayer => _currentPlayer == _player1 ? _player2 : _player1;

        public Game(Player player1, Player player2, List<Card> deck)
        {
            if (player1 == null || player2 == null)
            {
                throw new ArgumentException("Players should be passed!");
            }

            if (deck == null)
            {
                throw new ArgumentException("Cards which remain in deck should be passed!");
            }

            _deck = deck;
            _player1 = player1;
            _player2 = player2;
            _currentPlayer = player1;
        }

        private bool TurnIsRisky(ushort cardIndex)
        {
            if (_currentPlayer.KnowsAllAboutCard(cardIndex))
                return false;
            if (_currentPlayer.KnowsCardColor(cardIndex) &&
                CardCanHaveMoreThanOneRank(cardIndex))
                return true;
            if (_currentPlayer.KnowsCardRank(cardIndex) &&
                ExistsSuchColorThatPlayerCanLose(cardIndex))
                return true;
            return ExistsSuchCombinationThatPlayerCanLose(cardIndex);
        }

        private bool ExistsSuchCombinationThatPlayerCanLose(ushort cardIndex)
        {
            var unknownRanks = _currentPlayer.UnknownRanksFor(cardIndex).ToList();
            return _currentPlayer.UnknownColorsFor(cardIndex)
                .SelectMany(color => unknownRanks, (color, rank) => new { color, rank })
                .Select(colorRank => new Card(colorRank.color, colorRank.rank))
                .Any(_card => !CanBePlayed(_card));
        }

        private bool ExistsSuchColorThatPlayerCanLose(ushort cardIndex)
        {
            var card = _currentPlayer.GetCard(cardIndex);
            return _currentPlayer.UnknownColorsFor(cardIndex)
                .Select(color => new Card(color, card.Rank))
                .Any(_card => !CanBePlayed(_card));
        }

        private bool CardCanHaveMoreThanOneRank(ushort cardIndex)
        {
            return _currentPlayer.UnknownRanksFor(cardIndex).ToList().Count > 1;
        }

        public void PlayCard(ushort cardIndex)
        {
            var card = _currentPlayer.GetCard(cardIndex);
            if (CanBePlayed(card))
            {
                NumOfCorrectlyPlayedCards++;
                if (TurnIsRisky(cardIndex))
                {
                    NumOfRiskyTurns++;
                }

                _currentPlayer.PopCard(cardIndex);
                PutCardOnTable(card);
                _currentPlayer.AddCard(_deck.PopAt(0));
            }
            else
            {
                _over = true;
            }

            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        public void DropCard(ushort cardIndex)
        {
            _currentPlayer.PopCard(cardIndex);
            _currentPlayer.AddCard(_deck.PopAt(0));
            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        public void TellColor(CardColor proposedColor, List<ushort> cardIndexes)
        {
            var colorsOfOtherPlayerCardsWithGivenIndexes =
                cardIndexes.Select(_otherPlayer.GetCard).Select(card => card.Color);
            var otherPlayerCardsWithProposedColor =
                _otherPlayer.Cards.Select(card => card.Color).Where(color => color.Equals(proposedColor)).ToList();
            bool tipIsValid = colorsOfOtherPlayerCardsWithGivenIndexes.All(color => color.Equals(proposedColor)) &&
                              //check that given information about cards color is full
                              otherPlayerCardsWithProposedColor.Count == cardIndexes.Count;
            if (tipIsValid)
            {
                _otherPlayer.LearnColorFor(proposedColor, cardIndexes);
            }
            else
            {
                _over = true;
            }

            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        public void TellRank(ushort proposedRank, List<ushort> cardIndexes)
        {
            var ranksOfOtherPlayerCardsWithGivenIndexes =
                cardIndexes.Select(_otherPlayer.GetCard).Select(card => card.Rank);
            var otherPlayerCardsWithProposedRank =
                _otherPlayer.Cards.Select(card => card.Rank).Where(rank => rank == proposedRank).ToList();
            bool tipIsValid = ranksOfOtherPlayerCardsWithGivenIndexes.All(rank => rank == proposedRank) &&
                              //check that given information about cards rank is full
                              otherPlayerCardsWithProposedRank.Count == cardIndexes.Count;
            if (tipIsValid)
            {
                _otherPlayer.LearnRankFor(proposedRank, cardIndexes);
            }
            else
            {
                _over = true;
            }

            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        private bool TableHasMaxNumberOfCards()
        {
            return _table.Values.Sum(maxrank => maxrank) >= 25;
        }

        private bool CanBePlayed(Card card)
        {
            return !TableHasSameCard(card) && (card.Rank == 1 || TableHasCardWithSameColorAndRankLowerOn1(card));
        }

        private void PutCardOnTable(Card card)
        {
            _table[card.Color]++;
        }

        private bool TableHasSameCard(Card card)
        {
            return _table[card.Color] >= card.Rank;
        }

        private bool TableHasCardWithSameColorAndRankLowerOn1(Card card)
        {
            return _table[card.Color] == card.Rank - 1;
        }

        private void ChangeCurrentPlayerAndIncNumOfTurns()
        {
            ChangeCurrentPlayer();
            NumberOfTurns++;
        }

        private void ChangeCurrentPlayer()
        {
            _currentPlayer = _currentPlayer == _player1 ? _player2 : _player1;
        }
    }

    internal class Player
    {
        #region Static help members

        private static Dictionary<CardColor, bool?> CreateColorKnowledgesDict()
        {
            return CardUtils.CardColors.ToDictionary<CardColor, CardColor, bool?>(color => color, color => null);
        }

        private static Dictionary<ushort, bool?> CreateRankKnowledgesDict()
        {
            return CardUtils.CardRanks.ToDictionary<ushort, ushort, bool?>(rank => rank, rank => null);
        }

        private static readonly ImmutableList<ushort> AllCardIndexes =
            Enumerable.Range(0, 5).Select(rank => (ushort)rank).ToImmutableList();

        #endregion

        /// <summary>
        /// List represents knowledges about particular cards' colors in player's hands (thus we have list). <br/>
        /// Each dictionary represents knowledge about particular card's color. <br/>
        /// For each card in player's hands we have dictionary with key is a card' color (each color should be in each dictionary),
        /// and value is one of these values: 
        /// <list type="Bullet">
        /// <item>
        ///     <term>null</term>
        ///     <description>Means that we don't know whether card's color is such (key value) or not</description>
        /// </item>
        /// <item>
        ///     <term>true</term>
        ///     <description>We sure that card has such color</description>
        /// </item>
        /// <item>
        ///     <term>false</term>
        ///     <description>We sure that card's color is not such</description>
        /// </item>
        /// </list>
        /// </summary>
        private readonly List<Dictionary<CardColor, bool?>> _colorKnowledges;

        /// <summary>
        /// Same as <code>_colorKnowledges</code>
        /// but represents knowledges about card' ranks.
        /// <seealso cref="_colorKnowledges"/>
        /// </summary>
        private readonly List<Dictionary<ushort, bool?>> _rankKnowledges;

        public List<Card> Cards { get; }

        public Player(List<Card> cards)
        {
            if (cards == null || cards.Count != 5)
                throw new ArgumentException("Player should have 5 cards");
            Cards = cards;
            _colorKnowledges = Enumerable.Range(1, CardUtils.CardColors.Count)
                .Select(i => CreateColorKnowledgesDict()).ToList();
            _rankKnowledges = Enumerable.Range(1, CardUtils.CardRanks.Count)
                .Select(i => CreateRankKnowledgesDict()).ToList();
        }

        #region cards management

        public void AddCard(Card card)
        {
            Cards.Add(card);
            _colorKnowledges.Add(CreateColorKnowledgesDict());
            _rankKnowledges.Add(CreateRankKnowledgesDict());
        }

        public Card GetCard(ushort cardIndex)
        {
            return Cards[cardIndex];
        }

        public Card PopCard(ushort cardIndex)
        {
            _colorKnowledges.RemoveAt(cardIndex);
            _rankKnowledges.RemoveAt(cardIndex);
            return Cards.PopAt(cardIndex);
        }

        #endregion

        #region knowledges about cards

        public void LearnColorFor(CardColor proposedColor, IEnumerable<ushort> cardIndexes)
        {
            cardIndexes = cardIndexes.ToList();
            //save actual color for cards with given indexes
            foreach (var cardIndex in cardIndexes)
            {
                _colorKnowledges[cardIndex][proposedColor] = true;
                foreach (var otherColor in CardUtils.CardColors.Except(new[] { proposedColor }))
                {
                    _colorKnowledges[cardIndex][otherColor] = false;
                }
            }

            //now we know that other card's colors are not such
            foreach (var otherCardIndex in AllCardIndexes.Except(cardIndexes))
            {
                _colorKnowledges[otherCardIndex][proposedColor] = false;
            }
        }

        public void LearnRankFor(ushort proposedRank, IEnumerable<ushort> cardIndexes)
        {
            cardIndexes = cardIndexes.ToList();
            foreach (var cardIndex in cardIndexes)
            {
                _rankKnowledges[cardIndex][proposedRank] = true;
                foreach (var otherRank in CardUtils.CardRanks.Except(new[] { proposedRank }))
                {
                    _rankKnowledges[cardIndex][otherRank] = false;
                }
            }

            foreach (var otherCardIndex in AllCardIndexes.Except(cardIndexes))
            {
                _rankKnowledges[otherCardIndex][proposedRank] = false;
            }
        }

        public bool KnowsAllAboutCard(ushort cardIndex)
        {
            return KnowsCardColor(cardIndex) && KnowsCardRank(cardIndex);
        }

        public bool KnowsCardColor(ushort cardIndex)
        {
            return _colorKnowledges[cardIndex].Select(pair => pair.Value)
                .Where(colorKnown => colorKnown.HasValue)
                .Select(colorKnown => colorKnown.Value)
                .Any(colorKnown => colorKnown);
        }

        public bool KnowsCardRank(ushort cardIndex)
        {
            return _rankKnowledges[cardIndex].Select(pair => pair.Value)
                .Where(rankKnown => rankKnown.HasValue)
                .Select(rankKnown => rankKnown.Value)
                .Any(rankKnown => rankKnown);
        }

        public IEnumerable<CardColor> UnknownColorsFor(ushort cardIndex)
        {
            return _colorKnowledges[cardIndex].Where(pair => !pair.Value.HasValue).Select(pair => pair.Key);
        }

        public IEnumerable<ushort> UnknownRanksFor(ushort cardIndex)
        {
            return _rankKnowledges[cardIndex].Where(pair => !pair.Value.HasValue).Select(pair => pair.Key);
        }

        #endregion

    }

    internal class Card
    {
        public Card(CardColor color, ushort rank)
        {
            this.Color = color;
            if (!(GameConstants.MinCardRank <= rank && rank <= GameConstants.MaxCardRank))
            {
                throw new ArgumentException("Invalid card rank");
            }

            this.Rank = rank;
        }

        public readonly CardColor Color;
        public readonly ushort Rank;

        public override string ToString()
        {
            return $"{Color.ToString()[0]}{Rank}";
        }
    }

    internal enum CardColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        White
    }

    internal class GameConstants
    {
        public const ushort MinCardRank = 1;
        public const ushort MaxCardRank = 5;
    }

    internal static class CardUtils
    {
        public static readonly ISet<CardColor> CardColors =
            Enum.GetValues(typeof(CardColor)).Cast<CardColor>().ToImmutableSortedSet();

        public static readonly ISet<ushort> CardRanks =
            Enumerable.Range(GameConstants.MinCardRank, GameConstants.MaxCardRank)
                .Select(rank => (ushort)rank)
                .ToImmutableSortedSet();

        public static CardColor? ParseColor(this string cardColorName)
        {
            return cardColorName[0].ParseColor();
        }

        public static CardColor? ParseColor(this char cardColorMarker)
        {
            switch (cardColorMarker)
            {
                case 'R':
                    return CardColor.Red;
                case 'G':
                    return CardColor.Green;
                case 'B':
                    return CardColor.Blue;
                case 'Y':
                    return CardColor.Yellow;
                case 'W':
                    return CardColor.White;
                default:
                    return null;
            }
        }

        public static ushort ParseRank(this string cardRankString)
        {
            ushort rank = 0;
            ushort.TryParse(cardRankString, out rank);
            return rank;
        }

        public static Card ParseCard(this string cardDescription)
        {
            if (cardDescription == null || cardDescription.Length != 2)
            {
                return null;
            }

            var cardColor = cardDescription[0].ParseColor();
            if (!cardColor.HasValue)
            {
                return null;
            }

            var cardRank = (ushort)char.GetNumericValue(cardDescription[1]);
            if (!(1 <= cardRank && cardRank <= 5))
            {
                return null;
            }

            return new Card(cardColor.Value, cardRank);
        }
    }

    internal static class ListUtils
    {
        /// <summary>
        /// Remove element under specified index from list and return it
        /// </summary>
        /// <returns>Removed element</returns>
        public static T PopAt<T>(this List<T> list, int index)
        {
            var result = list[index];
            list.RemoveAt(index);
            return result;
        }
    }

}