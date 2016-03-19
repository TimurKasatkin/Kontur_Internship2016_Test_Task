using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KonturInternshipTeskTask
{
    public class Hanabi
    {
        #region static part

        #region turn description' templates and appropriate actions

        private static bool OverOrNotExists(Game game)
        {
            return game?.Over ?? true;
        }

        #region start new game 

        static readonly Regex StartGameCommandRegex =
            new Regex("^Start new game with deck (?<deck>((?<card>[RGBWY][1-5]) ?){11,})$");

        static Game HandleStartGameCommand(string turnDescription, Game currentGame)
        {
            string[] cardDescriptions =
                StartGameCommandRegex.Match(turnDescription).Groups["deck"].Value.Split();
            var player1Cards = cardDescriptions.Take(5).Select(CardUtils.ParseCard).ToList();
            var player2Cards = cardDescriptions.Skip(5).Take(5).Select(CardUtils.ParseCard).ToList();
            var deck = cardDescriptions.Skip(10).Select(CardUtils.ParseCard).ToList();
            return new Game(new Player(player1Cards), new Player(player2Cards), deck);
        }

        #endregion

        #region play card

        static readonly Regex PlayCardCommandRegex = new Regex(@"Play card (?<card_index>\d+)");

        static Game HandlePlayCardCommand(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var cardIndex = ushort.Parse(PlayCardCommandRegex.Match(turnDescription).Groups["card_index"].Value);
                currentGame.PlayCard(cardIndex);
            }
            return currentGame;
        }

        #endregion

        #region drop card

        static readonly Regex DropCardCommandRegex = new Regex(@"Drop card (?<card_index>\d+)");

        static Game HandleDropCardCommand(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var cardIndex = ushort.Parse(DropCardCommandRegex.Match(turnDescription).Groups["card_index"].Value);
                currentGame.DropCard(cardIndex);
            }
            return currentGame;
        }

        #endregion

        #region tell color

        static readonly Regex TellColorCommandRegex =
            new Regex(
                @"Tell color (?<color>Red|Green|Blue|White|Yellow) for cards (?<card_indexes>((?<card_index>\d+) ?)+)");

        static Game HandleTellColorCommand(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var match = TellColorCommandRegex.Match(turnDescription);
                var proposedColor = match.Groups["color"].Value.ParseColor().Value;
                var cardIndexes = match.Groups["card_indexes"].Value.Split()
                    .Select(ushort.Parse).ToList();
                currentGame.TellColor(proposedColor, cardIndexes);
            }
            return currentGame;
        }

        #endregion

        #region tell rank

        static readonly Regex TellRankCommandRegex =
            new Regex(@"Tell rank (?<card_rank>[1-5]) for cards (?<card_indexes>(\d+ ?)+)");

        static Game HandleTellRankCommand(string turnDescription, Game currentGame)
        {
            if (!OverOrNotExists(currentGame))
            {
                var match = TellRankCommandRegex.Match(turnDescription);
                var proposedRank = match.Groups["card_rank"].Value.ParseRank();
                var cardIndexes = match.Groups["card_indexes"].Value.Split()
                    .Select(ushort.Parse).ToList();
                currentGame.TellRank(proposedRank, cardIndexes);
            }
            return currentGame;
        }

        #endregion

        #region connection between turn description and actual action

        /// <summary>
        /// Key - command template (regexp)
        /// Value - function, which accepts turn description as string, current game 
        /// and returns new game (if start was requested) or the same game
        /// </summary>
        private readonly Dictionary<Regex, Func<string, Game, Game>> _commandActionDict = new Dictionary
            <Regex, Func<string, Game, Game>>
        {
            {StartGameCommandRegex, HandleStartGameCommand},
            {PlayCardCommandRegex, HandlePlayCardCommand},
            {DropCardCommandRegex, HandleDropCardCommand},
            {TellColorCommandRegex, HandleTellColorCommand},
            {TellRankCommandRegex, HandleTellRankCommand}
        };


        private Func<string, Game, Game> GetTurnBy(string turnDescription)
        {
            return _commandActionDict
                .FirstOrDefault(turnRegexActionPair => turnRegexActionPair.Key.IsMatch(turnDescription ?? ""))
                .Value;
        }

        #endregion

        #endregion

        #endregion

        #region non static part

        private Game currentGame;

        public bool CurrentGameExists => currentGame != null;

        public bool CurrentGameOver => currentGame.Over;

        public void Play()
        {
            string turnDescription;
            while ((turnDescription = Console.ReadLine()) != null)
            {
                //choose appropriate turn, perform it and  
                currentGame = GetTurnBy(turnDescription)?.Invoke(turnDescription, currentGame) ?? currentGame;
//                Console.WriteLine("==========================");
//                Console.WriteLine(currentGame);
//                Console.WriteLine("==========================");
                if (CurrentGameExists && CurrentGameOver)
                {
                    Console.WriteLine("Turn: {0}, cards: {1}, with risk: {2}",
                        currentGame.NumberOfTurns, currentGame.NumOfCorrectlyPlayedCards, currentGame.NumOfRiskyTurns);
                    currentGame = null;
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

        public bool Over
        {
            get { return _over || TableHasMaxNumberOfCards() || _deck.Count == 0; }
        }

        private List<Card> _deck;

        private Dictionary<CardColor, ushort> _table = new Dictionary<CardColor, ushort>
        {
            {CardColor.Red, 0},
            {CardColor.Green, 0},
            {CardColor.Blue, 0},
            {CardColor.Yellow, 0},
            {CardColor.White, 0}
        };

        #region players information

        private readonly Player _player1, _player2;

        private Player _currentPlayer;

        private Player _otherPlayer => _currentPlayer == _player1 ? _player2 : _player1;

        #endregion

        public Game(Player player1, Player player2, List<Card> deck)
        {
            if (player1 == null || player2 == null)
            {
                throw new ArgumentException("You should pass players!");
            }
            if (deck == null)
            {
                throw new ArgumentException("You should pass cards which remaining in deck!");
            }
            _deck = deck;
            _player1 = player1;
            _player2 = player2;
            _currentPlayer = player1;
        }

        public void PlayCard(ushort cardIndex)
        {
            var card = _currentPlayer.GetCard(cardIndex);

            if (CanBePlayed(card))
            {
                NumOfCorrectlyPlayedCards++;

                //TODO think about risky moves
                if (!_currentPlayer.KnowsAllAboutCard(cardIndex))
                {
                    if (_currentPlayer.KnowsCardColor(cardIndex))
                    {
                        if (_currentPlayer.UnknownRanks.ToList().Count > 1)
                        {
                            NumOfRiskyTurns++;
                        }
                    }
                    else if (_currentPlayer.KnowsCardRank(cardIndex))
                    {
                        if (_currentPlayer.UnknownColors
                            .Select(color => new Card(color, card.Rank))
                            .Any(_card => !CanBePlayed(_card)))
                        {
                            NumOfRiskyTurns++;
                        }
                    }
                    else
                    {
                        var unknownRanks = _currentPlayer.UnknownRanks.ToList();
                        if (_currentPlayer.UnknownColors
                            .SelectMany(color => unknownRanks,
                                (color, rank) => new {color, rank})
                            .Select(colorRank => new Card(colorRank.color, colorRank.rank))
                            .Any(_card => !CanBePlayed(_card)))
                        {
                            NumOfRiskyTurns++;
                        }
                    }
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
            var colorsOfOtherPlayerCardsWithGivenIndexes = cardIndexes
                .Select(_otherPlayer.GetCard)
                .Select(card => card.Color);
            var otherPlayerCardsWithProposedColor = _otherPlayer.Cards
                .Select(card => card.Color)
                .Where(color => color.Equals(proposedColor)).ToList();
            bool tipIsValid =
                colorsOfOtherPlayerCardsWithGivenIndexes.All(color => color.Equals(proposedColor)) &&
                //check that given information about cards color is full
                otherPlayerCardsWithProposedColor.Count == cardIndexes.Count;
            if (tipIsValid)
            {
                _otherPlayer.TellColorFor(proposedColor, cardIndexes);
            }
            else
            {
                _over = true;
            }
            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        public void TellRank(ushort proposedRank, List<ushort> cardIndexes)
        {
            var ranksOfOtherPlayerCardsWithGivenIndexes = cardIndexes
                .Select(_otherPlayer.GetCard)
                .Select(card => card.Rank);
            var otherPlayerCardsWithProposedRank = _otherPlayer.Cards
                .Select(card => card.Rank)
                .Where(rank => rank == proposedRank).ToList();
            bool tipIsValid =
                ranksOfOtherPlayerCardsWithGivenIndexes.All(rank => rank == proposedRank) &&
                //check that given information about cards rank is full
                otherPlayerCardsWithProposedRank.Count == cardIndexes.Count;
            if (tipIsValid)
            {
                _otherPlayer.TellRankFor(proposedRank, cardIndexes);
            }
            else
            {
                _over = true;
            }
            ChangeCurrentPlayerAndIncNumOfTurns();
        }

        #region private methods

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

        #endregion

        public override string ToString()
        {
            return $"Turn: {NumberOfTurns}, Score: {NumOfCorrectlyPlayedCards}, _over: {_over}, Over: {Over}\n" +
                   $"Deck: {string.Join(" ", _deck)}\n" +
                   $"Table: {string.Join(" ", _table.Select(pair => $"{pair.Key.ToString()[0]}{pair.Value}"))}\n" +
                   $"Player1: {_player1}\n" +
                   $"Player2: {_player2}\n" +
                   $"CurrentPlayer: {(_currentPlayer == _player1 ? "Player1" : "Player2")}\n";
        }
    }

    internal class Player
    {
        private List<CardColor?> _cardColors = new List<CardColor?>();
        private List<ushort> _cardRanks = new List<ushort>();

        public List<Card> Cards { get; }

        public Player(List<Card> cards)
        {
            if (cards == null || cards.Count != 5)
                throw new ArgumentException("Player should have 5 cards");
            Cards = cards;
            _cardColors.AddRange(Enumerable.Repeat<CardColor?>(null, cards.Count));
            _cardRanks.AddRange(Enumerable.Repeat<ushort>(0, cards.Count)); //0 means rank is unknown
        }

        public void AddCard(Card card)
        {
            Cards.Add(card);
            _cardColors.Add(null);
            _cardRanks.Add(0);
        }

        public bool CertainAboutCardColorAndRank(ushort cardIndex)
        {
            return _cardColors[cardIndex].HasValue && _cardRanks[cardIndex] != 0;
        }

        public Card GetCard(ushort cardIndex)
        {
            return Cards[cardIndex];
        }

        public Card PopCard(ushort cardIndex)
        {
            _cardColors.RemoveAt(cardIndex);
            _cardRanks.RemoveAt(cardIndex);
            return Cards.PopAt(cardIndex);
        }

        public void TellColorFor(CardColor color, ushort cardIndex)
        {
            _cardColors[cardIndex] = color;
        }

        public void TellColorFor(CardColor color, IEnumerable<ushort> cardIndexes)
        {
            foreach (var cardIndex in cardIndexes)
            {
                _cardColors[cardIndex] = color;
            }
        }

        public void TellRankFor(ushort proposedRank, IEnumerable<ushort> cardIndexes)
        {
            foreach (var cardIndex in cardIndexes)
            {
                _cardRanks[cardIndex] = proposedRank;
            }
        }

        public IEnumerable<CardColor> KnownColors
            => _cardColors.Where(color => color.HasValue).Select(color => color.Value);

        public IEnumerable<CardColor> UnknownColors => CardUtils.CardColors.Except(KnownColors);

        public IEnumerable<ushort> KnownRanks => _cardRanks.Where(rank => rank != 0);

        public IEnumerable<ushort> UnknownRanks
            => Enumerable.Range(1, 5).Select(rank => (ushort) rank).Except(KnownRanks);

        public bool KnowsAllAboutCard(ushort cardIndex)
        {
            return KnowsCardColor(cardIndex) && KnowsCardRank(cardIndex);
        }

        public bool KnowsCardColor(ushort cardIndex)
        {
            return _cardColors[cardIndex].HasValue;
        }

        public bool KnowsCardRank(ushort cardIndex)
        {
            return _cardRanks[cardIndex] != 0;
        }

        public override string ToString()
        {
            return $"Cards: {string.Join(" ", Cards)}";
        }
    }

    internal class Card
    {
        public Card(CardColor color, ushort rank)
        {
            this.Color = color;
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

    internal static class CardUtils
    {
        public static readonly ISet<CardColor> CardColors;

        static CardUtils()
        {
            CardColors = new HashSet<CardColor>(Enum.GetValues(typeof (CardColor)).Cast<CardColor>());
        }

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
            var cardRank = (ushort) char.GetNumericValue(cardDescription[1]);
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