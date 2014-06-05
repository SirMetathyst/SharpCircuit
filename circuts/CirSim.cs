// CirSim.java (c) 2010 by Paul Falstad
// For information about the theory behind this, see Electronic Circuit & System Simulation Methods by Pillage
// http://www.falstad.com/circuit/

using System;
using System.Collections;
using System.Collections.Generic;

namespace Circuts {

	public class CirSim {

		System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

		public Random random;
		public static int sourceRadius = 7;
		public static double freqMult = 3.14159265 * 2 * 4;

		public bool stoppedCheck;
		public bool smallGridCheckItem;
		public bool conventionCheckItem;
		public double speedBar = Math.Log(10 * 14.3) * 24 + 61.5; // 14.3
		public double currentBar = 50;
		public double powerBar = 50;
		public static double pi = 3.14159265358979323846;
		public int gridSize, gridMask, gridRound;
		public bool analyzeFlag;
		public bool dumpMatrix;
		public double t;
		public int hintType = -1, hintItem1, hintItem2;
		public String stopMessage;
		public double timeStep = 5e-6;
		public static int HINT_LC = 1;
		public static int HINT_RC = 2;
		public static int HINT_3DB_C = 3;
		public static int HINT_TWINT = 4;
		public static int HINT_3DB_L = 5;
		public List<CircuitElm> elmList = new List<CircuitElm>();
		public CircuitElm dragElm, menuElm, mouseElm, stopElm;
		public bool didSwitch = false;
		public int mousePost = -1;
		public CircuitElm plotXElm, plotYElm;
		public int draggingPost;
		public SwitchElm heldSwitchElm;
		public double[][] circuitMatrix; 
		public double[] circuitRightSide; 
		public double[] origRightSide;
		public double[][] origMatrix;
		public RowInfo[] circuitRowInfo;
		public int[] circuitPermute;
		public bool circuitNonLinear;
		public int voltageSourceCount;
		public int circuitMatrixSize, circuitMatrixFullSize;
		public bool circuitNeedsMap;
		public int scopeCount;
		public Scope[] scopes;
		public int[] scopeColCount;
		public Type[] dumpTypes, shortcuts;
		public static String muString = "u";
		public static String ohmString = "ohm";

		public string[] info;

		public class FindPathInfo {
			CirSim root;
			
			public static int INDUCT = 1;
			public static int VOLTAGE = 2;
			public static int SHORT = 3;
			public static int CAP_V = 4;
			public bool[] used;
			public int dest;
			public CircuitElm firstElm;
			public int type;
			
			public FindPathInfo(CirSim r,int t, CircuitElm e, int d) {
				root = r;
				dest = d;
				type = t;
				firstElm = e;
				used = new bool[root.nodeList.Count];
			}
			
			public bool findPath(int n1) {
				return findPath(n1, -1);
			}
			
			public bool findPath(int n1, int depth) {
				if (n1 == dest) {
					return true;
				}
				if (depth-- == 0) {
					return false;
				}
				if (used[n1]) {
					// System.out.println("used " + n1);
					return false;
				}
				used[n1] = true;
				int i;
				for (i = 0; i != root.elmList.Count; i++) {
					CircuitElm ce = root.getElm(i);
					if (ce == firstElm) {
						continue;
					}
					if (type == INDUCT) {
						if (ce is CurrentElm) {
							continue;
						}
					}
					if (type == VOLTAGE) {
						if (!(ce.isWire() || ce is VoltageElm)) {
							continue;
						}
					}
					if (type == SHORT && !ce.isWire()) {
						continue;
					}
					if (type == CAP_V) {
						if (!(ce.isWire() || ce is CapacitorElm || ce is VoltageElm)) {
							continue;
						}
					}
					if (n1 == 0) {
						// look for posts which have a ground connection;
						// our path can go through ground
						int z;
						for (z = 0; z != ce.getPostCount(); z++) {
							if (ce.hasGroundConnection(z)
							    && findPath(ce.getNode(z), depth)) {
								used[n1] = false;
								return true;
							}
						}
					}
					int j;
					for (j = 0; j != ce.getPostCount(); j++) {
						// System.out.println(ce + " " + ce.getNode(j));
						if (ce.getNode(j) == n1) {
							break;
						}
					}
					if (j == ce.getPostCount()) {
						continue;
					}
					if (ce.hasGroundConnection(j) && findPath(0, depth)) {
						// System.out.println(ce + " has ground");
						used[n1] = false;
						return true;
					}
					if (type == INDUCT && ce is InductorElm) {
						double c = ce.getCurrent();
						if (j == 0) {
							c = -c;
						}
						// System.out.println("matching " + c + " to " +
						// firstElm.getCurrent());
						// System.out.println(ce + " " + firstElm);
						if (Math.Abs(c - firstElm.getCurrent()) > 1e-10) {
							continue;
						}
					}
					int k;
					for (k = 0; k != ce.getPostCount(); k++) {
						if (j == k) {
							continue;
						}
						// System.out.println(ce + " " + ce.getNode(j) + "-" +
						// ce.getNode(k));
						if (ce.getConnection(j, k)
						    && findPath(ce.getNode(k), depth)) {
							// System.out.println("got findpath " + n1);
							used[n1] = false;
							return true;
						}
						// System.out.println("back on findpath " + n1);
					}
				}
				used[n1] = false;
				// System.out.println(n1 + " failed");
				return false;
			}
		}

		public CirSim(){
			watch.Start();
		}

		public int getrand(int x) {
			int q = random.Next();
			if (q < 0) {
				q = -q;
			}
			return q % x;
		}

		public static int resct = 6;
		public long lastTime = 0, lastFrameTime, lastIterTime, secTime = 0;
		public int frames = 0;
		public int steps = 0;
		public int framerate = 0, steprate = 0;

		public void updateCircuit() {

			CircuitElm realMouseElm;

			if (analyzeFlag) {
				analyzeCircuit();
				analyzeFlag = false;
			}

			realMouseElm = mouseElm;
			if (mouseElm == null) {
				mouseElm = stopElm;
			}

			if (!stoppedCheck) {
				try {
					runCircuit();
				} catch (Exception) {
					analyzeFlag = true;
					return;
				}
			}

			if (!stoppedCheck) {
				long sysTime = watch.ElapsedMilliseconds;
				if (lastTime != 0) {
					int inc = (int) (sysTime - lastTime);
					double c = currentBar;
					c = Math.Exp(c / 3.5 - 14.2);
					CircuitElm.currentMult = 1.7 * inc * c;
					if (!conventionCheckItem) {
						CircuitElm.currentMult = -CircuitElm.currentMult;
					}
				}
				if (sysTime - secTime >= 1000) {
					framerate = frames;
					steprate = steps;
					frames = 0;
					steps = 0;
					secTime = sysTime;
				}
				lastTime = sysTime;
			} else {
				lastTime = 0;
			}

			CircuitElm.powerMult = Math.Exp(powerBar / 4.762 - 7);

			int i = 0;
			int badnodes = 0;
			// find bad connections, nodes not connected to other elements which
			// intersect other elements' bounding boxes
			// debugged by hausen: nullPointerException
			if (nodeList != null) {
				for (i = 0; i != nodeList.Count; i++) {
					CircuitNode cn = getCircuitNode(i);
					if (!cn.@internal && cn.links.Count == 1) {
						int bb = 0, j;
						CircuitNodeLink cnl = cn.links[0];
						for (j = 0; j != elmList.Count; j++) { // TODO: (hausen) see if this change does not break stuff
							CircuitElm ce = getElm(j);
							if (cnl.elm != ce) { //&& getElm(j).boundingBox.contains(cn.x, cn.y) // && (getElm(j).x == cn.x && getElm(j).y == cn.y)
								bb++;
							}
						}
						if (bb > 0) {
							badnodes++;
						}
					}
				}
			}


			if (stopMessage != null) {

			} else {

				info = new String[10];
				if (mouseElm != null) {
					if (mousePost == -1) {
						mouseElm.getInfo(info);
					} else {
						info[0] = "V = " + CircuitElm.getUnitText(mouseElm.getPostVoltage(mousePost), "V");
						// //shownodes for (i = 0; i != mouseElm.getPostCount();
						// i++) info[0] += " " + mouseElm.nodes[i]; if
						// (mouseElm.getVoltageSourceCount() > 0) info[0] += ";" +
						// (mouseElm.getVoltageSource()+nodeList.Count);
					}

				} else {

					info[0] = "t = " + CircuitElm.getUnitText(t, "s");

				}
				if (hintType != -1) {
					for (i = 0; info[i] != null; i++) {
						;
					}
					String s = getHint();
					if (s == null) {
						hintType = -1;
					} else {
						info[i] = s;
					}
				}

				for (i = 0; info[i] != null; i++) {
					;
				}
				if (badnodes > 0) {
					info[i++] = badnodes + ((badnodes == 1) ? " bad connection" : " bad connections");
				}
				
			}

			mouseElm = realMouseElm;
			frames++;

			if (!stoppedCheck && circuitMatrix != null) {
				// Limit to 50 fps (thanks to Jurgen Klotzer for this)
				long delay = 1000 / 50 - (watch.ElapsedMilliseconds - lastFrameTime);
				// realg.drawString("delay: " + delay, 10, 90);
				if (delay > 0) {
					try {
						//Thread.sleep(delay);
					} catch (Exception) {
					}
				}
			}
			lastFrameTime = lastTime;
		}

		public String getHint() {
			CircuitElm c1 = getElm(hintItem1);
			CircuitElm c2 = getElm(hintItem2);
			if (c1 == null || c2 == null) {
				return null;
			}
			if (hintType == HINT_LC) {
				if (!(c1 is InductorElm)) {
					return null;
				}
				if (!(c2 is CapacitorElm)) {
					return null;
				}
				InductorElm ie = (InductorElm) c1;
				CapacitorElm ce = (CapacitorElm) c2;
				return "res.f = " + CircuitElm.getUnitText(1 / (2 * pi * Math.Sqrt(ie.inductance* ce.capacitance)), "Hz");
			}
			if (hintType == HINT_RC) {
				if (!(c1 is ResistorElm)) {
					return null;
				}
				if (!(c2 is CapacitorElm)) {
					return null;
				}
				ResistorElm re = (ResistorElm) c1;
				CapacitorElm ce = (CapacitorElm) c2;
				return "RC = " + CircuitElm.getUnitText(re.resistance * ce.capacitance,"s");
			}
			if (hintType == HINT_3DB_C) {
				if (!(c1 is ResistorElm)) {
					return null;
				}
				if (!(c2 is CapacitorElm)) {
					return null;
				}
				ResistorElm re = (ResistorElm) c1;
				CapacitorElm ce = (CapacitorElm) c2;
				return "f.3db = " + CircuitElm.getUnitText(1 / (2 * pi * re.resistance * ce.capacitance),"Hz");
			}
			if (hintType == HINT_3DB_L) {
				if (!(c1 is ResistorElm)) {
					return null;
				}
				if (!(c2 is InductorElm)) {
					return null;
				}
				ResistorElm re = (ResistorElm) c1;
				InductorElm ie = (InductorElm) c2;
				return "f.3db = "+ CircuitElm.getUnitText(re.resistance / (2 * pi * ie.inductance), "Hz");
			}
			if (hintType == HINT_TWINT) {
				if (!(c1 is ResistorElm)) {
					return null;
				}
				if (!(c2 is CapacitorElm)) {
					return null;
				}
				ResistorElm re = (ResistorElm) c1;
				CapacitorElm ce = (CapacitorElm) c2;
				return "fc = "+ CircuitElm.getUnitText(1 / (2 * pi * re.resistance * ce.capacitance),"Hz");
			}
			return null;
		}

		public void toggleSwitch(int n) {
			int i;
			for (i = 0; i != elmList.Count; i++) {
				CircuitElm ce = getElm(i);
				if (ce is SwitchElm) {
					n--;
					if (n == 0) {
						((SwitchElm) ce).toggle();
						analyzeFlag = true;
						return;
					}
				}
			}
		}

		void needAnalyze() {
			analyzeFlag = true;
		}

		public List<CircuitNode> nodeList = new List<CircuitNode>();
		public CircuitElm[] voltageSources;

		public CircuitNode getCircuitNode(int n) {
			if (n >= nodeList.Count) {
				return null;
			}
			return nodeList[n];
		}

		public CircuitElm getElm(int n) {
			if (n >= elmList.Count) {
				return null;
			}
			return elmList[n];
		}

		public void analyzeCircuit() {

			if (elmList.Count == 0) {
				return;
			}

			stopMessage = null;
			stopElm = null;
			int i, j;
			int vscount = 0;
			nodeList = new List<CircuitNode>();
			bool gotGround = false;
			bool gotRail = false;
			CircuitElm volt = null;

			// System.out.println("ac1");
			// look for voltage or ground element
			for (i = 0; i != elmList.Count; i++) {
				CircuitElm ce = getElm(i);
				if (ce is GroundElm) {
					gotGround = true;
					break;
				}
				if (ce is RailElm) {
					gotRail = true;
				}
				if (volt == null && ce is VoltageElm) {
					volt = ce;
				}
			}

			// if no ground, and no rails, then the voltage elm's first terminal is ground
			if (!gotGround && volt != null && !gotRail) {
				CircuitNode cn = new CircuitNode();
				Point pt = volt.getPost(0);
				cn.x = pt.x;
				cn.y = pt.y;
				nodeList.Add(cn);
			} else {
				// otherwise allocate extra node for ground
				CircuitNode cn = new CircuitNode();
				cn.x = cn.y = -1;
				nodeList.Add(cn);
			}
			// System.out.println("ac2");

			// allocate nodes and voltage sources
			for (i = 0; i != elmList.Count; i++) {
				CircuitElm ce = getElm(i);
				int inodes = ce.getInternalNodeCount();
				int ivs = ce.getVoltageSourceCount();
				int posts = ce.getPostCount();

				// allocate a node for each post and match posts to nodes
				for (j = 0; j != posts; j++) {
					Point pt = ce.getPost(j);
					int k;
					for (k = 0; k != nodeList.Count; k++) {
						CircuitNode cn = getCircuitNode(k);
						if (pt.x == cn.x && pt.y == cn.y) {
							break;
						}
					}
					if (k == nodeList.Count) {
						CircuitNode cn = new CircuitNode();
						cn.x = pt.x;
						cn.y = pt.y;
						CircuitNodeLink cnl = new CircuitNodeLink();
						cnl.num = j;
						cnl.elm = ce;
						cn.links.Add(cnl);
						ce.setNode(j, nodeList.Count);
						nodeList.Add(cn);
					} else {
						CircuitNodeLink cnl = new CircuitNodeLink();
						cnl.num = j;
						cnl.elm = ce;
						getCircuitNode(k).links.Add(cnl);
						ce.setNode(j, k);
						// if it's the ground node, make sure the node voltage is 0,
						// cause it may not get set later
						if (k == 0) {
							ce.setNodeVoltage(j, 0);
						}
					}
				}
				for (j = 0; j != inodes; j++) {
					CircuitNode cn = new CircuitNode();
					cn.x = cn.y = -1;
					cn.@internal = true;
					CircuitNodeLink cnl = new CircuitNodeLink();
					cnl.num = j + posts;
					cnl.elm = ce;
					cn.links.Add(cnl);
					ce.setNode(cnl.num, nodeList.Count);
					nodeList.Add(cn);
				}
				vscount += ivs;
			}
			voltageSources = new CircuitElm[vscount];
			vscount = 0;
			circuitNonLinear = false;
			// System.out.println("ac3");

			// determine if circuit is nonlinear
			for (i = 0; i != elmList.Count; i++) {
				CircuitElm ce = getElm(i);
				if (ce.nonLinear()) {
					circuitNonLinear = true;
				}
				int ivs = ce.getVoltageSourceCount();
				for (j = 0; j != ivs; j++) {
					voltageSources[vscount] = ce;
					ce.setVoltageSource(j, vscount++);
				}
			}
			voltageSourceCount = vscount;

			int matrixSize = nodeList.Count - 1 + vscount;

			circuitMatrix = new double[matrixSize][]; //matrixSize
			for (int z = 0; z < matrixSize; z++)
				circuitMatrix[z] = new double[matrixSize];

			circuitRightSide = new double[matrixSize];

			origMatrix = new double[matrixSize][];
			for (int z = 0; z < matrixSize; z++)
				origMatrix[z] = new double[matrixSize];

			origRightSide = new double[matrixSize];
			circuitMatrixSize = circuitMatrixFullSize = matrixSize;
			circuitRowInfo = new RowInfo[matrixSize];
			circuitPermute = new int[matrixSize];
			for (i = 0; i != matrixSize; i++) {
				circuitRowInfo[i] = new RowInfo();
			}
			circuitNeedsMap = false;

			// stamp linear circuit elements
			for (i = 0; i != elmList.Count; i++) {
				CircuitElm ce = getElm(i);
				ce.stamp();
			}
			// System.out.println("ac4");

			// determine nodes that are unconnected
			bool[] closure = new bool[nodeList.Count];
			bool changed = true;
			closure[0] = true;
			while (changed) {
				changed = false;
				for (i = 0; i != elmList.Count; i++) {
					CircuitElm ce = getElm(i);
					// loop through all ce's nodes to see if they are connected
					// to other nodes not in closure
					for (j = 0; j < ce.getPostCount(); j++) {
						if (!closure[ce.getNode(j)]) {
							if (ce.hasGroundConnection(j)) {
								closure[ce.getNode(j)] = changed = true;
							}
							continue;
						}
						int k;
						for (k = 0; k != ce.getPostCount(); k++) {
							if (j == k) {
								continue;
							}
							int kn = ce.getNode(k);
							if (ce.getConnection(j, k) && !closure[kn]) {
								closure[kn] = true;
								changed = true;
							}
						}
					}
				}
				if (changed) {
					continue;
				}

				// connect unconnected nodes
				for (i = 0; i != nodeList.Count; i++) {
					if (!closure[i] && !getCircuitNode(i).@internal) {
						//System.out.println("node " + i + " unconnected");
						stampResistor(0, i, 1e8);
						closure[i] = true;
						changed = true;
						break;
					}
				}
			}
			// System.out.println("ac5");

			for (i = 0; i != elmList.Count; i++) {
				CircuitElm ce = getElm(i);
				// look for inductors with no current path
				if (ce is InductorElm) {
					FindPathInfo fpi = new FindPathInfo(this,FindPathInfo.INDUCT, ce, ce.getNode(1));
					// first try findPath with maximum depth of 5, to avoid slowdowns
					if (!fpi.findPath(ce.getNode(0), 5) && !fpi.findPath(ce.getNode(0))) {
						//System.out.println(ce + " no path");
						ce.reset();
					}
				}
				// look for current sources with no current path
				if (ce is CurrentElm) {
					FindPathInfo fpi = new FindPathInfo(this,FindPathInfo.INDUCT, ce,ce.getNode(1));
					if (!fpi.findPath(ce.getNode(0))) {
						stop("No path for current source!", ce);
						return;
					}
				}
				// look for voltage source loops
				if ((ce is VoltageElm && ce.getPostCount() == 2) || ce is WireElm) {
					FindPathInfo fpi = new FindPathInfo(this,FindPathInfo.VOLTAGE, ce,ce.getNode(1));
					if (fpi.findPath(ce.getNode(0))) {
						stop("Voltage source/wire loop with no resistance!", ce);
						return;
					}
				}
				// look for shorted caps, or caps w/ voltage but no R
				if (ce is CapacitorElm) {
					FindPathInfo fpi = new FindPathInfo(this,FindPathInfo.SHORT, ce, ce.getNode(1));
					if (fpi.findPath(ce.getNode(0))) {
						//System.out.println(ce + " shorted");
						ce.reset();
					} else {
						fpi = new FindPathInfo(this,FindPathInfo.CAP_V, ce, ce.getNode(1));
						if (fpi.findPath(ce.getNode(0))) {
							stop("Capacitor loop with no resistance!", ce);
							return;
						}
					}
				}
			}
			// System.out.println("ac6");

			// simplify the matrix; this speeds things up quite a bit
			for (i = 0; i != matrixSize; i++) {
				int qm = -1, qp = -1;
				double qv = 0;
				RowInfo re = circuitRowInfo[i];
				// System.out.println("row " + i + " " + re.lsChanges + " " + re.rsChanges + " " + re.dropRow);
				if (re.lsChanges || re.dropRow || re.rsChanges) {
					continue;
				}
				double rsadd = 0;

				// look for rows that can be removed
				for (j = 0; j != matrixSize; j++) {
					double q = circuitMatrix[i][j];
					if (circuitRowInfo[j].type == RowInfo.ROW_CONST) {
						// keep a running total of const values that have been
						// removed already
						rsadd -= circuitRowInfo[j].value * q;
						continue;
					}
					if (q == 0) {
						continue;
					}
					if (qp == -1) {
						qp = j;
						qv = q;
						continue;
					}
					if (qm == -1 && q == -qv) {
						qm = j;
						continue;
					}
					break;
				}
				// System.out.println("line " + i + " " + qp + " " + qm + " " + j);
				/*
				 * if (qp != -1 && circuitRowInfo[qp].lsChanges) {
				 * System.out.println("lschanges"); continue; } if (qm != -1 &&
				 * circuitRowInfo[qm].lsChanges) { System.out.println("lschanges");
				 * continue; }
				 */
				if (j == matrixSize) {
					if (qp == -1) {
						stop("Matrix error", null);
						return;
					}
					RowInfo elt = circuitRowInfo[qp];
					if (qm == -1) {
						// we found a row with only one nonzero entry; that value
						// is a constant
						int k;
						for (k = 0; elt.type == RowInfo.ROW_EQUAL && k < 100; k++) {
							// follow the chain
							// System.out.println("following equal chain from " + i + " " + qp + " to " + elt.nodeEq);
							qp = elt.nodeEq;
							elt = circuitRowInfo[qp];
						}
						if (elt.type == RowInfo.ROW_EQUAL) {
							// break equal chains
							// System.out.println("Break equal chain");
							elt.type = RowInfo.ROW_NORMAL;
							continue;
						}
						if (elt.type != RowInfo.ROW_NORMAL) {
							//System.out.println("type already " + elt.type + " for " + qp + "!");
							continue;
						}
						elt.type = RowInfo.ROW_CONST;
						elt.value = (circuitRightSide[i] + rsadd) / qv;
						circuitRowInfo[i].dropRow = true;
						// System.out.println(qp + " * " + qv + " = const " + elt.value);
						i = -1; // start over from scratch
					} else if (circuitRightSide[i] + rsadd == 0) {
						// we found a row with only two nonzero entries, and one
						// is the negative of the other; the values are equal
						if (elt.type != RowInfo.ROW_NORMAL) {
							// System.out.println("swapping");
							int qq = qm;
							qm = qp;
							qp = qq;
							elt = circuitRowInfo[qp];
							if (elt.type != RowInfo.ROW_NORMAL) {
								// we should follow the chain here, but this
								// hardly ever happens so it's not worth worrying
								// about
								//System.out.println("swap failed");
								continue;
							}
						}
						elt.type = RowInfo.ROW_EQUAL;
						elt.nodeEq = qm;
						circuitRowInfo[i].dropRow = true;
						// System.out.println(qp + " = " + qm);
					}
				}
			}
			// System.out.println("ac7");

			// find size of new matrix
			int nn = 0;
			for (i = 0; i != matrixSize; i++) {
				RowInfo elt = circuitRowInfo[i];
				if (elt.type == RowInfo.ROW_NORMAL) {
					elt.mapCol = nn++;
					// System.out.println("col " + i + " maps to " + elt.mapCol);
					continue;
				}
				if (elt.type == RowInfo.ROW_EQUAL) {
					RowInfo e2 = null;
					// resolve chains of equality; 100 max steps to avoid loops
					for (j = 0; j != 100; j++) {
						e2 = circuitRowInfo[elt.nodeEq];
						if (e2.type != RowInfo.ROW_EQUAL) {
							break;
						}
						if (i == e2.nodeEq) {
							break;
						}
						elt.nodeEq = e2.nodeEq;
					}
				}
				if (elt.type == RowInfo.ROW_CONST) {
					elt.mapCol = -1;
				}
			}
			for (i = 0; i != matrixSize; i++) {
				RowInfo elt = circuitRowInfo[i];
				if (elt.type == RowInfo.ROW_EQUAL) {
					RowInfo e2 = circuitRowInfo[elt.nodeEq];
					if (e2.type == RowInfo.ROW_CONST) {
						// if something is equal to a const, it's a const
						elt.type = e2.type;
						elt.value = e2.value;
						elt.mapCol = -1;
						// System.out.println(i + " = [late]const " + elt.value);
					} else {
						elt.mapCol = e2.mapCol;
						// System.out.println(i + " maps to: " + e2.mapCol);
					}
				}
			}
			// System.out.println("ac8");

			/*
			 * System.out.println("matrixSize = " + matrixSize);
			 * 
			 * for (j = 0; j != circuitMatrixSize; j++) { System.out.println(j +
			 * ": "); for (i = 0; i != circuitMatrixSize; i++)
			 * System.out.print(circuitMatrix[j][i] + " "); System.out.print("  " +
			 * circuitRightSide[j] + "\n"); } System.out.print("\n");
			 */

			// make the new, simplified matrix
			int newsize = nn;
			double[][] newmatx = new double[newsize][]; // newsize
			for(int z = 0;z < newsize;z++){
				newmatx[z] = new double[newsize];
			}

			double[] newrs = new double[newsize];
			int ii = 0;
			for (i = 0; i != matrixSize; i++) {
				RowInfo rri = circuitRowInfo[i];
				if (rri.dropRow) {
					rri.mapRow = -1;
					continue;
				}
				newrs[ii] = circuitRightSide[i];
				rri.mapRow = ii;
				// System.out.println("Row " + i + " maps to " + ii);
				for (j = 0; j != matrixSize; j++) {
					RowInfo ri = circuitRowInfo[j];
					if (ri.type == RowInfo.ROW_CONST) {
						newrs[ii] -= ri.value * circuitMatrix[i][j];
					} else {
						newmatx[ii][ri.mapCol] += circuitMatrix[i][j];
					}
				}
				ii++;
			}

			circuitMatrix = newmatx;
			circuitRightSide = newrs;
			matrixSize = circuitMatrixSize = newsize;
			for (i = 0; i != matrixSize; i++) {
				origRightSide[i] = circuitRightSide[i];
			}
			for (i = 0; i != matrixSize; i++) {
				for (j = 0; j != matrixSize; j++) {
					origMatrix[i][j] = circuitMatrix[i][j];
				}
			}
			circuitNeedsMap = true;

			/*
			 * System.out.println("matrixSize = " + matrixSize + " " +
			 * circuitNonLinear); for (j = 0; j != circuitMatrixSize; j++) { for (i
			 * = 0; i != circuitMatrixSize; i++)
			 * System.out.print(circuitMatrix[j][i] + " "); System.out.print("  " +
			 * circuitRightSide[j] + "\n"); } System.out.print("\n");
			 */

			// if a matrix is linear, we can do the lu_factor here instead of
			// needing to do it every frame
			if (!circuitNonLinear) {
				if (!lu_factor(circuitMatrix, circuitMatrixSize, circuitPermute)) {
					stop("Singular matrix!", null);
					return;
				}
			}
		}

		public void stop(String s, CircuitElm ce) {
			stopMessage = s;
			circuitMatrix = null;
			stopElm = ce;
			stoppedCheck = true;
			analyzeFlag = false;
		}

		// control voltage source vs with voltage from n1 to n2 (must
		// also call stampVoltageSource())
		public void stampVCVS(int n1, int n2, double coef, int vs) {
			int vn = nodeList.Count + vs;
			stampMatrix(vn, n1, coef);
			stampMatrix(vn, n2, -coef);
		}

		// stamp independent voltage source #vs, from n1 to n2, amount v
		public void stampVoltageSource(int n1, int n2, int vs, double v) {
			int vn = nodeList.Count + vs;
			stampMatrix(vn, n1, -1);
			stampMatrix(vn, n2, 1);
			stampRightSide(vn, v);
			stampMatrix(n1, vn, 1);
			stampMatrix(n2, vn, -1);
		}

		// use this if the amount of voltage is going to be updated in doStep()
		public void stampVoltageSource(int n1, int n2, int vs) {
			int vn = nodeList.Count + vs;
			stampMatrix(vn, n1, -1);
			stampMatrix(vn, n2, 1);
			stampRightSide(vn);
			stampMatrix(n1, vn, 1);
			stampMatrix(n2, vn, -1);
		}

		public void updateVoltageSource(int n1, int n2, int vs, double v) {
			int vn = nodeList.Count + vs;
			stampRightSide(vn, v);
		}

		public void stampResistor(int n1, int n2, double r) {
			double r0 = 1 / r;
			if (Double.IsNaN(r0) || Double.IsInfinity(r0)) {
				//System.out.print("bad resistance " + r + " " + r0 + "\n");
				int a = 0;
				a /= a;
			}
			stampMatrix(n1, n1, r0);
			stampMatrix(n2, n2, r0);
			stampMatrix(n1, n2, -r0);
			stampMatrix(n2, n1, -r0);
		}

		public void stampConductance(int n1, int n2, double r0) {
			stampMatrix(n1, n1, r0);
			stampMatrix(n2, n2, r0);
			stampMatrix(n1, n2, -r0);
			stampMatrix(n2, n1, -r0);
		}

		// current from cn1 to cn2 is equal to voltage from vn1 to 2, divided by g
		public void stampVCCurrentSource(int cn1, int cn2, int vn1, int vn2, double g) {
			stampMatrix(cn1, vn1, g);
			stampMatrix(cn2, vn2, g);
			stampMatrix(cn1, vn2, -g);
			stampMatrix(cn2, vn1, -g);
		}

		public void stampCurrentSource(int n1, int n2, double i) {
			stampRightSide(n1, -i);
			stampRightSide(n2, i);
		}

		// stamp a current source from n1 to n2 depending on current through vs
		public void stampCCCS(int n1, int n2, int vs, double gain) {
			int vn = nodeList.Count + vs;
			stampMatrix(n1, vn, gain);
			stampMatrix(n2, vn, -gain);
		}

		// stamp value x in row i, column j, meaning that a voltage change
		// of dv in node j will increase the current into node i by x dv.
		// (Unless i or j is a voltage source node.)
		public void stampMatrix(int i, int j, double x) {
			if (i > 0 && j > 0) {
				if (circuitNeedsMap) {
					i = circuitRowInfo[i - 1].mapRow;
					RowInfo ri = circuitRowInfo[j - 1];
					if (ri.type == RowInfo.ROW_CONST) {
						// System.out.println("Stamping constant " + i + " " + j + " " + x);
						circuitRightSide[i] -= x * ri.value;
						return;
					}
					j = ri.mapCol;
					// System.out.println("stamping " + i + " " + j + " " + x);
				} else {
					i--;
					j--;
				}
				circuitMatrix[i][j] += x;
			}
		}

		// stamp value x on the right side of row i, representing an
		// independent current source flowing into node i
		public void stampRightSide(int i, double x) {
			if (i > 0) {
				if (circuitNeedsMap) {
					i = circuitRowInfo[i - 1].mapRow;
					// System.out.println("stamping " + i + " " + x);
				} else {
					i--;
				}
				circuitRightSide[i] += x;
			}
		}

		// indicate that the value on the right side of row i changes in doStep()
		public void stampRightSide(int i) {
			// System.out.println("rschanges true " + (i-1));
			if (i > 0) {
				circuitRowInfo[i - 1].rsChanges = true;
			}
		}

		// indicate that the values on the left side of row i change in doStep()
		public void stampNonLinear(int i) {
			if (i > 0) {
				circuitRowInfo[i - 1].lsChanges = true;
			}
		}

		public double getIterCount() {
			if (speedBar == 0) {
				return 0;
			}
			// return (Math.exp((speedBar.getValue()-1)/24.) + .5);
			return 0.1 * Math.Exp((speedBar - 61) / 24.0);
		}

		public bool converged;
		public int subIterations;

		public void runCircuit() {
			if (circuitMatrix == null || elmList.Count == 0) {
				circuitMatrix = null;
				return;
			}
			int iter;
			// int maxIter = getIterCount();
			bool debugprint = dumpMatrix;
			dumpMatrix = false;
			long steprate = (long) (160 * getIterCount());
			long tm = watch.ElapsedMilliseconds;
			long lit = lastIterTime;
			if (1000 >= steprate * (tm - lastIterTime)) {
				return;
			}
			for (iter = 1;; iter++) {
				int i, j, k, subiter;
				for (i = 0; i != elmList.Count; i++) {
					CircuitElm ce = getElm(i);
					ce.startIteration();
				}
				steps++;
				int subiterCount = 5000;
				for (subiter = 0; subiter != subiterCount; subiter++) {
					converged = true;
					subIterations = subiter;
					for (i = 0; i != circuitMatrixSize; i++) {
						circuitRightSide[i] = origRightSide[i];
					}
					if (circuitNonLinear) {
						for (i = 0; i != circuitMatrixSize; i++) {
							for (j = 0; j != circuitMatrixSize; j++) {
								circuitMatrix[i][j] = origMatrix[i][j];
							}
						}
					}
					for (i = 0; i != elmList.Count; i++) {
						CircuitElm ce = getElm(i);
						ce.doStep();
					}
					if (stopMessage != null) {
						return;
					}
					bool printit = debugprint;
					debugprint = false;
					for (j = 0; j != circuitMatrixSize; j++) {
						for (i = 0; i != circuitMatrixSize; i++) {
							double x = circuitMatrix[i][j];
							if (Double.IsNaN(x) || Double.IsInfinity(x)) {
								stop("nan/infinite matrix!", null);
								return;
							}
						}
					}
					if (printit) {
						for (j = 0; j != circuitMatrixSize; j++) {
							for (i = 0; i != circuitMatrixSize; i++) {
								//System.out.print(circuitMatrix[j][i] + ",");
							}
							//System.out.print("  " + circuitRightSide[j] + "\n");
						}
						//System.out.print("\n");
					}
					if (circuitNonLinear) {
						if (converged && subiter > 0) {
							break;
						}
						if (!lu_factor(circuitMatrix, circuitMatrixSize,
								circuitPermute)) {
							stop("Singular matrix!", null);
							return;
						}
					}
					lu_solve(circuitMatrix, circuitMatrixSize, circuitPermute,
							circuitRightSide);

					for (j = 0; j != circuitMatrixFullSize; j++) {
						RowInfo ri = circuitRowInfo[j];
						double res = 0;
						if (ri.type == RowInfo.ROW_CONST) {
							res = ri.value;
						} else {
							res = circuitRightSide[ri.mapCol];
						}
						// System.out.println(j + " " + res + " " + ri.type + " " + ri.mapCol);
						if (Double.IsNaN(res)) {
							converged = false;
							// debugprint = true;
							break;
						}
						if (j < nodeList.Count - 1) {
							CircuitNode cn = getCircuitNode(j + 1);
							for (k = 0; k != cn.links.Count; k++) {
								CircuitNodeLink cnl = cn.links[k];
								cnl.elm.setNodeVoltage(cnl.num, res);
							}
						} else {
							int ji = j - (nodeList.Count - 1);
							// System.out.println("setting vsrc " + ji + " to " + res);
							voltageSources[ji].setCurrent(ji, res);
						}
					}
					if (!circuitNonLinear) {
						break;
					}
				}
				if (subiter > 5) {
					//System.out.print("converged after " + subiter + " iterations\n");
				}
				if (subiter == subiterCount) {
					stop("Convergence failed!", null);
					break;
				}
				t += timeStep;
				for (i = 0; i != scopeCount; i++) {
					//scopes[i].timeStep();
				}
				tm = watch.ElapsedMilliseconds;
				lit = tm;
				if (iter * 1000 >= steprate * (tm - lastIterTime)
						|| (tm - lastFrameTime > 500)) {
					break;
				}
			}
			lastIterTime = lit;
			// System.out.println((System.currentTimeMillis()-lastFrameTime)/(double)iter);
		}

		public int min(int a, int b) {
			return (a < b) ? a : b;
		}

		public int max(int a, int b) {
			return (a > b) ? a : b;
		}

		public int snapGrid(int x) {
			return (x + gridRound) & gridMask;
		}

		public bool doSwitch(int x, int y) {
			if (mouseElm == null || !(mouseElm is SwitchElm)) {
				return false;
			}
			SwitchElm se = (SwitchElm) mouseElm;
			se.toggle();
			if (se.momentary) {
				heldSwitchElm = se;
			}
			needAnalyze();
			return true;
		}

		public int locateElm(CircuitElm elm) {
			int i;
			for (i = 0; i != elmList.Count; i++) {
				if (elm == elmList[i]) {
					return i;
				}
			}
			return -1;
		}

		public int distanceSq(int x1, int y1, int x2, int y2) {
			x2 -= x1;
			y2 -= y1;
			return x2 * x2 + y2 * y2;
		}

		public void setGrid() {
			gridSize = (smallGridCheckItem) ? 8 : 16;
			gridMask = ~(gridSize - 1);
			gridRound = gridSize / 2 - 1;
		}

		// factors a matrix into upper and lower triangular matrices by
		// gaussian elimination. On entry, a[0..n-1][0..n-1] is the
		// matrix to be factored. ipvt[] returns an integer vector of pivot
		// indices, used in the lu_solve() routine.
		public bool lu_factor(double[][] a, int n, int[] ipvt) {
			double[] scaleFactors;
			int i, j, k;

			scaleFactors = new double[n];

			// divide each row by its largest element, keeping track of the
			// scaling factors
			for (i = 0; i != n; i++) {
				double largest = 0;
				for (j = 0; j != n; j++) {
					double x = Math.Abs(a[i][j]);
					if (x > largest) {
						largest = x;
					}
				}
				// if all zeros, it's a singular matrix
				if (largest == 0) {
					return false;
				}
				scaleFactors[i] = 1.0 / largest;
			}

			// use Crout's method; loop through the columns
			for (j = 0; j != n; j++) {

				// calculate upper triangular elements for this column
				for (i = 0; i != j; i++) {
					double q = a[i][j];
					for (k = 0; k != i; k++) {
						q -= a[i][k] * a[k][j];
					}
					a[i][j] = q;
				}

				// calculate lower triangular elements for this column
				double largest = 0;
				int largestRow = -1;
				for (i = j; i != n; i++) {
					double q = a[i][j];
					for (k = 0; k != j; k++) {
						q -= a[i][k] * a[k][j];
					}
					a[i][j] = q;
					double x = Math.Abs(q);
					if (x >= largest) {
						largest = x;
						largestRow = i;
					}
				}

				// pivoting
				if (j != largestRow) {
					double x;
					for (k = 0; k != n; k++) {
						x = a[largestRow][k];
						a[largestRow][k] = a[j][k];
						a[j][k] = x;
					}
					scaleFactors[largestRow] = scaleFactors[j];
				}

				// keep track of row interchanges
				ipvt[j] = largestRow;

				// avoid zeros
				if (a[j][j] == 0.0) {
					//System.out.println("avoided zero");
					a[j][j] = 1e-18;
				}

				if (j != n - 1) {
					double mult = 1.0 / a[j][j];
					for (i = j + 1; i != n; i++) {
						a[i][j] *= mult;
					}
				}
			}
			return true;
		}

		// Solves the set of n linear equations using a LU factorization
		// previously performed by lu_factor. On input, b[0..n-1] is the right
		// hand side of the equations, and on output, contains the solution.
		public void lu_solve(double[][] a, int n, int[] ipvt, double[] b) {
			int i;

			// find first nonzero b element
			for (i = 0; i != n; i++) {
				int row = ipvt[i];

				double swap = b[row];
				b[row] = b[i];
				b[i] = swap;
				if (swap != 0) {
					break;
				}
			}

			int bi = i++;
			for (; i < n; i++) {
				int row = ipvt[i];
				int j;
				double tot = b[row];

				b[row] = b[i];
				// forward substitution using the lower triangular matrix
				for (j = bi; j < i; j++) {
					tot -= a[i][j] * b[j];
				}
				b[i] = tot;
			}
			for (i = n - 1; i >= 0; i--) {
				double tot = b[i];

				// back-substitution using the upper triangular matrix
				int j;
				for (j = i + 1; j != n; j++) {
					tot -= a[i][j] * b[j];
				}
				b[i] = tot / a[i][i];
			}
		}

	}
}