using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum ScoreEvent{
	draw,
	mine,
	mineGold,
	gameWin,
	gameLoss
}


public class Prospector : MonoBehaviour {

	static public Prospector 	S;
	static public int SCORE_FROM_PREV_ROUND=0;
	static public int HIGH_SCORE = 0;
	public Deck					deck;
	public TextAsset			deckXML;
	public Vector3 layoutCenter;
	public float xOffset = 3;
	public float yOffset = -2.5f;
	public Transform layoutAnchor;

	public CardProspector target;
	public List<CardProspector> tableau;
	public List<CardProspector> discardPile;
	public List<CardProspector> drawPile;

	public int chain = 0;
	public int scoreRun = 0;
	public int score = 0;
	public Layout layout;
	public TextAsset layoutXML;

	void Awake(){
		S = this;
		if (PlayerPrefs.HasKey ("ProspectorHighScore")) {
			HIGH_SCORE = PlayerPrefs.GetInt("ProspectorHighScore");
		}
		score += SCORE_FROM_PREV_ROUND;
		SCORE_FROM_PREV_ROUND = 0;
	}



	void Start() {
		deck = GetComponent<Deck> ();
		deck.InitDeck (deckXML.text);
		Deck.Shuffle (ref deck.cards);

		layout = GetComponent<Layout> ();
		layout.ReadLayout (layoutXML.text);
		drawPile = ConvertListCardsToListCardProspectors(deck.cards);
		LayoutGame ();
	}

	CardProspector Draw(){
		CardProspector cd = drawPile [0];
		drawPile.RemoveAt (0);
		return cd;
	}

	public void CardClicked(CardProspector cd){
		switch (cd.state) {
		case CardState.target:
			break;
		case CardState.drawpile:
			MoveToDiscard(target);
			MoveToTarget(Draw());
			UpdateDrawPile();
			break;
		case CardState.tableau:
			bool validMatch = true;
			if(!cd.faceUp){
				validMatch = false;
			}
			if(!AdjacentRank(cd,target)){
				validMatch = false;
			}
			if(!validMatch){
				return;
			}
			tableau.Remove(cd);
			MoveToTarget(cd);
			SetTableauFaces();
			break;
		}
		CheckForGameOver ();
	}

	public bool AdjacentRank(CardProspector c0,CardProspector c1){
		if (!c0.faceUp || !c1.faceUp) {
			return false;
		}
		if (Mathf.Abs (c0.rank - c1.rank) == 1) {
			return true;
		}
		if (c0.rank == 1 && c1.rank == 13) {
			return true;
		}
		if (c0.rank == 13 && c1.rank == 1) {
			return true;
		}
		return false;
	}

	void LayoutGame(){
		if (layoutAnchor == null) {
			GameObject tGO = new GameObject("_LayoutAnchor");
			layoutAnchor = tGO.transform;
			layoutAnchor.transform.position = layoutCenter;
		}
		CardProspector cp;
		foreach (SlotDef tSD in layout.slotDefs) {
			cp = Draw();
			cp.faceUp = tSD.faceUp;
			cp.transform.parent = layoutAnchor;
			cp.transform.localPosition = new Vector3(
				layout.multiplier.x * tSD.x,
				layout.multiplier.y * tSD.y,
				-tSD.layerID);
			cp.layoutID = tSD.id;
			cp.slotDef = tSD;
			cp.state = CardState.tableau;
			cp.SetSortingLayerName(tSD.layerName);
			tableau.Add(cp);
		}

		foreach (CardProspector tCP in tableau) {
			foreach(int hid in tCP.slotDef.hiddenBy){
				cp = FindCardByLayoutID(hid);
				tCP.hiddenBy.Add(cp);
			}
		}
		MoveToTarget (Draw ());
		UpdateDrawPile ();
	}

	List<CardProspector> ConvertListCardsToListCardProspectors(List<Card> lCD){
		List<CardProspector> lCP = new List<CardProspector> ();
		CardProspector tCP;
		foreach (Card tCD in lCD) {
			tCP = tCD as CardProspector;
			lCP.Add(tCP);
		}
		return(lCP);
	}


	void MoveToDiscard(CardProspector cd){
		cd.state = CardState.discard;
		discardPile.Add (cd);
		cd.transform.parent = layoutAnchor;
		cd.transform.localPosition = new Vector3 (
			layout.multiplier.x * layout.discardPile.x,
			layout.multiplier.y * layout.discardPile.y,
			-layout.discardPile.layerID+0.5f);
		cd.faceUp = true;
		cd.SetSortingLayerName (layout.discardPile.layerName);
		cd.SetSortOrder (-100 + discardPile.Count);
	}

	void MoveToTarget(CardProspector cd){
		if (target != null) {
			MoveToDiscard (target);
		}
			target = cd;
			cd.state = CardState.target;
			cd.transform.parent = layoutAnchor;
			cd.transform.localPosition = new Vector3(
				layout.multiplier.x * layout.discardPile.x,
				layout.multiplier.y * layout.discardPile.y,
				-layout.discardPile.layerID);
			cd.faceUp = true;
			cd.SetSortingLayerName(layout.discardPile.layerName);
			cd.SetSortOrder(0);
	}

	void UpdateDrawPile(){
		CardProspector cd;
		for (int i=0; i< drawPile.Count; i++) {
			cd = drawPile[i];
			cd.transform.parent = layoutAnchor;
			Vector2 dpStagger = layout.drawPile.stagger;
			cd.transform.localPosition = new Vector3(
				layout.multiplier.x * (layout.multiplier.x + i*dpStagger.x),
				layout.multiplier.y * (layout.multiplier.y + i*dpStagger.y),
				-layout.drawPile.layerID+0.1f*i);
			cd.faceUp = false;
			cd.state = CardState.drawpile;
			cd.SetSortingLayerName(layout.drawPile.layerName);
			cd.SetSortOrder(-10*i);
		}
	}

	CardProspector FindCardByLayoutID(int layoutID){
		foreach (CardProspector tCP in tableau) {
			if(tCP.layoutID == layoutID){
				return tCP;
			}
		}
		return null;
	}

	void SetTableauFaces(){
		foreach (CardProspector cd in tableau) {
			bool fup = true;
			foreach(CardProspector cover in cd.hiddenBy){
				if(cover.state == CardState.tableau){
					fup = false;
				}
			}
			cd.faceUp = fup;
		}
	}

	void CheckForGameOver(){
		if (tableau.Count == 0) {
			GameOver(true);
			return;
		}
		if (drawPile.Count > 0) {
			return;
		}
		foreach (CardProspector cd in tableau) {
			if(AdjacentRank(cd, target)){
				return;
			}
		}
		GameOver (false);
	}

	void GameOver(bool won){
		if (won) {
			print ("Game Over. You won!");
		} else {
			print ("Game Over. You lost.");
		}
		Application.LoadLevel ("__Prospector_Scene_0");
	}

















}
