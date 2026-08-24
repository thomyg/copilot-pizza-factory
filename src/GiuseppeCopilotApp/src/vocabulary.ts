/**
 * One switch between two ways of saying the same thing.
 *
 * Nothing behind this changes: the same service window, the same rota, the same requisitions
 * awaiting the same signatures. What changes is the room you are standing in. "Nonna's desk"
 * and "the pineapple order" land beautifully at a conference and cost you the meeting in a
 * procurement review — not because the process is wrong for them, but because the vocabulary
 * says it is not about them.
 *
 * So the labels move and the machinery does not, which is also the honest claim: if flipping a
 * word list turns the demo into a staffing and purchasing back office, it always was one.
 *
 * Modelled on the Engine Room's existing Suits/Nerds toggle — same idea, one flight level down.
 */
export type Vocabulary = 'trattoria' | 'enterprise';

export interface IVocabulary {
  /** What the whole thing is called. */
  house: string;
  /** The trading window — a service, or a business day. */
  service: string;
  serviceOpen: string;
  serviceShut: string;
  /** Who does the work. */
  staff: string;
  roster: string;
  shift: string;
  /** Buying things. */
  requisition: string;
  requisitions: string;
  supplier: string;
  invoice: string;
  /** The stuff being bought. */
  stock: string;
  /** People asking for days off. */
  timeOff: string;
  /** The back office persona's job title, when one is needed. */
  backOffice: string;
  /** What the front-of-house numbers are counting. */
  demand: string;
  unit: string;
  units: string;
}

const TRATTORIA: IVocabulary = {
  house: 'Trattoria Giuseppe',
  service: 'service',
  serviceOpen: 'Service open',
  serviceShut: 'Between services',
  staff: 'brigade',
  roster: 'rota',
  shift: 'shift',
  requisition: 'purchase order',
  requisitions: 'purchase orders',
  supplier: 'supplier',
  invoice: 'invoice',
  stock: 'pantry',
  timeOff: 'time off',
  backOffice: 'back office',
  demand: 'covers',
  unit: 'pizza',
  units: 'pizzas'
};

const ENTERPRISE: IVocabulary = {
  house: 'Operations',
  service: 'operating window',
  serviceOpen: 'Operating',
  serviceShut: 'Outside operating hours',
  staff: 'workforce',
  roster: 'shift roster',
  shift: 'shift',
  requisition: 'requisition',
  requisitions: 'requisitions',
  supplier: 'vendor',
  invoice: 'invoice',
  stock: 'inventory',
  timeOff: 'absence request',
  backOffice: 'shared services',
  demand: 'transactions',
  unit: 'order line',
  units: 'order lines'
};

export function vocabularyFor(mode: Vocabulary | undefined): IVocabulary {
  return mode === 'enterprise' ? ENTERPRISE : TRATTORIA;
}

/**
 * Sentence case for a label that may start a heading. Kept here so every surface capitalises
 * the same way rather than each one guessing.
 */
export function titleCase(label: string): string {
  return label.length === 0 ? label : label[0].toUpperCase() + label.slice(1);
}
