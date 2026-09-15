import { PlanDetail } from "@/features/plans/plan-detail";
export default async function PlanDetailPage({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <PlanDetail id={id}/>; }
