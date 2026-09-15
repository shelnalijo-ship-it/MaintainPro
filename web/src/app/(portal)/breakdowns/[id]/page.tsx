import { BreakdownDetailView } from "@/features/breakdowns/breakdown-detail";
export default async function BreakdownDetailPage({params}:{params:Promise<{id:string}>}){const {id}=await params;return <BreakdownDetailView id={id}/>}
