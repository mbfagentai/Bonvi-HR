/**
 * Closed option lists of the HR-F02 form, in the paper blank's order.
 *
 * Values are the server's names (SPEC §3). Labels come from `kiosk.*` keys
 * derived with `lowerFirst`; `scripts/check-locales.mjs` reads these arrays,
 * so a value added here without its four translations fails the check.
 */

export const HOUSING_TYPES = ['Apartment', 'House'] as const

export const MARITAL_OPTIONS = ['Married', 'Unmarried', 'Divorced', 'Widowed'] as const

export const MILITARY_OPTIONS = ['Served', 'Reserve', 'Deferred', 'NotServed'] as const

export const DRIVER_LICENSE_CATEGORIES = ['A', 'B', 'C', 'D', 'BE', 'CE', 'DE'] as const

export const EDUCATION_OPTIONS = [
  'SecondarySchool',
  'VocationalSchool',
  'HigherIncomplete',
  'HigherComplete',
  'HigherMaster',
] as const

/** The four fixed language rows; «Boshqa» rows are added on demand (at most 3). */
export const FIXED_LANGUAGES = ['Uzbek', 'Russian', 'English', 'Chinese'] as const

/** The three level columns of the language table. */
export const LANGUAGE_ASPECTS = ['speaking', 'reading', 'writing'] as const

export const FORM_LANGUAGE_NAMES = ['Uzbek', 'Russian', 'English', 'Chinese', 'Other'] as const

export const FAMILY_RELATIONS = [
  'Father',
  'Mother',
  'Spouse',
  'Brother',
  'Sister',
  'Child',
  'Other',
] as const

/**
 * Computer skill groups (SPEC §3.4, shortened 2026-10-26: the owner cut the
 * paper's long table — the company hires mostly operators, welders and
 * office staff). The programming column and the rarely used items are gone;
 * the candidate names them in `computerSkillsOther`.
 */
export const COMPUTER_GROUPS = ['os', 'office', 'special'] as const
export type ComputerGroup = (typeof COMPUTER_GROUPS)[number]

/** Every skill key the server accepts, in the form's order; any other key is a 400. */
export const COMPUTER_SKILL_KEYS = [
  'windows',
  'mobileOs',
  'msWord',
  'msExcel',
  'msPowerPoint',
  'googleWorkspace',
  'oneC',
  'didox',
  'crm',
  'autocad',
  'photoshop',
] as const
export type ComputerSkillKey = (typeof COMPUTER_SKILL_KEYS)[number]

export const COMPUTER_SKILLS: Readonly<Record<ComputerGroup, readonly ComputerSkillKey[]>> = {
  os: ['windows', 'mobileOs'],
  office: ['msWord', 'msExcel', 'msPowerPoint', 'googleWorkspace'],
  special: ['oneC', 'didox', 'crm', 'autocad', 'photoshop'],
}

/** Levels of the paper's scale: 1 – beginner … 4 – expert; no level = does not know. */
export const COMPUTER_LEVELS = [1, 2, 3, 4] as const

export function isComputerSkillKey(value: string): value is ComputerSkillKey {
  return (COMPUTER_SKILL_KEYS as readonly string[]).includes(value)
}

/** Section 10 — open questions. The first four are required. */
export const ABOUT_QUESTIONS = [
  'motivation',
  'whyUs',
  'strengths',
  'weaknesses',
  'motivators',
  'successDefinition',
  'lastJobAchievement',
  'bestIdea',
  'biggestChallenge',
  'stressHandling',
  'unfairnessToYou',
  'unfairnessToCompany',
  'teamwork',
  'previousTeamOpinion',
  'studentWorkExperience',
  'fiveYearPlan',
  'extraNote',
] as const
export type AboutQuestion = (typeof ABOUT_QUESTIONS)[number]

export const REQUIRED_ABOUT: readonly AboutQuestion[] = ['motivation', 'whyUs', 'strengths', 'weaknesses']

/** Section 11 — every consent must be ticked. */
export const CONSENTS = [
  'consentAccuracy',
  'consentPersonalData',
  'consentReferenceCheck',
  'consentNotifyChanges',
  'consentConfidentiality',
] as const
export type ConsentKey = (typeof CONSENTS)[number]

/**
 * Wizard screens — short, one topic each, so a screen fits a 390 × 844 phone
 * with little scrolling (SPEC §7, owner: the six long steps looked
 * «rasvoroq»). In the blank's order; titles are `kiosk.screen.<name>`.
 * The blank's gender, passport and JSHSHIR rows are not asked (SPEC §3.1,
 * 2026-10-26: HR takes the passport only after the council interview).
 */
export const WIZARD_SCREENS = [
  'vacancy',
  'personal',
  'address',
  'contacts',
  'marital',
  'license',
  'education',
  'school',
  'languages',
  'computer',
  'work',
  'references',
  'family',
  'referral',
  'salary',
  'conditions',
  'terms',
  'health',
  'background',
  'about',
  'aboutSuccess',
  'aboutHard',
  'aboutTeam',
  'consent',
  'signature',
] as const
export type WizardScreen = (typeof WIZARD_SCREENS)[number]

/**
 * Which answers each screen owns — by root name, both the draft's
 * (`vacancy`, `about.<key>`, `consents.<key>`) and the server's field paths
 * (`vacancyId`, `familyMembers[0].fullName`, `motivation`). Validation runs
 * over the whole form and each screen shows only its own errors, so the rules
 * themselves stay in one place (`validateAll`).
 */
export const SCREEN_FIELDS: Readonly<Record<WizardScreen, readonly string[]>> = {
  vacancy: ['vacancy', 'vacancyId', 'photo'],
  personal: ['lastName', 'firstName', 'middleName', 'birthDate'],
  address: ['registeredAddress', 'actualAddress', 'housingType', 'mahalla'],
  contacts: ['phone', 'phoneExtra', 'telegram', 'email'],
  marital: ['maritalStatus', 'childrenCount', 'militaryService', 'militaryServiceMonths'],
  license: ['hasDriverLicense', 'driverLicenseCategories', 'hasCar', 'carModel'],
  education: ['educationLevel', 'educations'],
  school: ['schoolName', 'strongSubjects', 'courses'],
  languages: ['languages'],
  computer: ['computerSkills', 'computerSkillsOther'],
  work: ['jobs', 'totalExperienceYears', 'totalExperienceMonths'],
  references: ['references'],
  family: ['familyMembers'],
  referral: ['referralChannelId', 'referralDetail', 'hasAcquaintances', 'acquaintancesDetail'],
  salary: ['lastSalary', 'expectedSalary', 'availableFrom', 'hasLoan', 'loanMonthlyPayment'],
  conditions: ['canWorkShifts', 'canWorkOvertime', 'canTravel', 'agreesToTrial'],
  terms: ['hasOtherJob', 'otherJobDetail', 'agreesParentsInvite', 'startConditions'],
  health: ['hasHealthRestriction', 'healthRestrictionDetail', 'hasFamilyPatient'],
  background: [
    'hasConviction',
    'convictionDetail',
    'relativesConviction',
    'relativesConvictionDetail',
    'abroadTrips',
  ],
  about: ['motivation', 'whyUs', 'strengths', 'weaknesses'],
  aboutSuccess: ['motivators', 'successDefinition', 'lastJobAchievement', 'bestIdea'],
  aboutHard: ['biggestChallenge', 'stressHandling', 'unfairnessToYou', 'unfairnessToCompany'],
  aboutTeam: ['teamwork', 'previousTeamOpinion', 'studentWorkExperience', 'fiveYearPlan', 'extraNote'],
  consent: [...CONSENTS],
  signature: ['signatureName', 'signature'],
}

/** The open questions of one «O'zingiz haqingizda» screen, in the paper's order. */
export function aboutQuestionsOf(screen: WizardScreen): readonly AboutQuestion[] {
  return ABOUT_QUESTIONS.filter((key) => SCREEN_FIELDS[screen].includes(key))
}

/** `InProgress` → `inProgress` — the same conversion `check-locales.mjs` applies. */
export function lowerFirst(value: string): string {
  return value.charAt(0).toLowerCase() + value.slice(1)
}
